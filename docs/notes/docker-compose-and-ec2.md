# Docker Compose と EC2 の基本メモ

## このプロジェクトのコンテナ構成

```text
インターネット
  → AWS Security GroupがTCP 8080を許可
  → EC2ホストの8080番ポート
  → Composeのポート公開 8080:8080
  → game-api（ASP.NET Core）の8080番ポート
      ├─ game-db（PostgreSQL）
      └─ game-redis（Redis）
```

Security Groupは「インターネットからEC2へ届く通信」を許可するAWS側のファイアウォール。今回、受信ルールでTCP 8080を許可しているため、EC2へ届いた8080番通信をComposeの`ports: "8080:8080"`がAPIコンテナへ転送する。

PostgreSQLとRedisはComposeに`ports`指定がないため、Security Groupでポートを開けても外部公開されない。Composeネットワーク内でAPIからだけ接続する。

```yaml
ConnectionStrings__GameDb: "Host=db;Port=5432;..."
Redis__Configuration: "redis:6379,..."
```

`db`と`redis`は、`container_name`ではなくComposeの**サービス名**。Composeがコンテナ内DNSを用意する。

## `docker compose up --build -d`

```bash
docker compose up --build -d
```

- `up`: Compose定義に沿ってコンテナを作成・起動する
- `--build`: DockerfileからAPIイメージを作り直す。C#、Dockerfile、`.csproj`変更後に必要
- `-d`: detach。ターミナルを占有せず裏で起動する

通常のソース変更では`docker compose down`は不要。

```bash
docker compose down -v
```

の`-v`はVolumeも削除するため、PostgreSQLデータを消す可能性がある。学習DBを残したい場合は使わない。

## 起動確認

```bash
docker compose ps
```

Compose配下の状態を確認する。期待値は、APIとDBが`running`、Redisが`healthy`。

```bash
docker compose logs --tail=50 api
docker logs -f game-api
```

- `--tail=50`: 末尾50行だけ表示
- `-f`: follow。新しく出るログを継続表示する。止めるときは`Ctrl + C`
- `api`はComposeサービス名、`game-api`はコンテナ名

## `depends_on` とhealthcheck

```yaml
depends_on:
  redis:
    condition: service_healthy
```

これはAPIを起動する前に、Redisのhealthcheck成功を待つ指定。

```yaml
test: ["CMD", "redis-cli", "ping"]
```

`CMD`はコンテナ内で`redis-cli ping`を実行する指定。`PONG`が返り終了コード0ならRedisは`healthy`になる。

```text
Redisプロセス起動 → started → healthcheck成功 → healthy → API起動
```

`service_started`はプロセス起動済み、`service_healthy`は接続できる状態まで確認済み、という違いがある。`depends_on`は起動時の順序だけを制御し、後からRedisが落ちてもAPIを自動再起動するものではない。

## EC2内のDBとマネージドDBの違い

現在は、1台のEC2という仮想マシンの中でDockerを動かし、その中にAPI・PostgreSQL・Redisの各コンテナを置いている。

```text
AWSのEC2（自分でOSを管理するIaaS）
  └─ Docker Compose
      ├─ APIコンテナ
      ├─ PostgreSQLコンテナ
      └─ Redisコンテナ
```

この構成では、DBのアップデート、バックアップ、メモリ・ディスク容量、障害復旧も自分で管理する。一方、RDSなどのマネージドDBを使うと、DBプロセスはEC2の外に置かれ、AWSが運用作業の多くを担う。APIコンテナだけをEC2に残す構成も可能になる。

インデックスとDBサイズは「呼び出し側」ではなくDBが持つ。`CREATE INDEX`やEF Core Migrationで定義すると、PostgreSQLのデータ領域にインデックス用データも保存される。APIはSQLを発行するだけで、どのインデックスを使うかは基本的にPostgreSQLが判断する。

## Dockerビルドキャッシュとディスク容量

Dockerfileの各工程は、次回ビルドで再利用できる途中結果としてBuildKitのキャッシュに残る。今のDockerfileでは`COPY . .`の後に`dotnet publish`するため、C#を変更するとその後のpublishは再実行される。

```text
SDKイメージ
  → COPY . .
  → dotnet publish
  → APIイメージ
```

EC2の8 GiBルートVolumeで、ビルドキャッシュとイメージが増えた結果、空き容量が約86 MiB・使用率99%になり、SSHやビルドが不安定になった。`df -h`でOS全体の空き容量、`docker system df -v`でDockerのイメージ・Volume・ビルドキャッシュの内訳を確認する。

Dockerの保存先は、Docker Engineのデータルートである`/var/lib/docker`が基本だが、Dockerのバージョン・設定によっては、イメージレイヤーやスナップショットをcontainerdが`/var/lib/containerd`に保持する。今回のEC2でも、`/var/lib/docker`は約94 MiB、`/var/lib/containerd`は約2.5 GiBだった。

```text
/var/lib/docker
  └─ Docker Engineのメタデータ、コンテナ、Volume、BuildKit関連データなど

/var/lib/containerd
  └─ イメージ・ビルド途中結果に使われるレイヤーやスナップショットなど
```

ビルドキャッシュは「完成済みイメージのキャッシュ」だけではない。`COPY`や`RUN dotnet publish`など、Dockerfile途中工程の再利用用データも含む。領域の内訳はディレクトリ容量だけで正確に区別しづらいため、Dockerに`docker system df -v`で管理上の内訳を確認させる。

```bash
docker info -f '{{.DockerRootDir}}'
sudo du -xhd1 /var/lib/docker /var/lib/containerd | sort -h
```

1行目はDocker Engineのデータルート、2行目は実ディスク上でどちらが大きいかを確認する。`/var/lib/docker`や`/var/lib/containerd`配下を直接削除してはいけない。Dockerの管理情報と実体がずれ、コンテナやイメージが壊れる可能性がある。

```bash
docker builder prune -af
```

- `builder prune`: ビルドキャッシュだけを削除する
- `-a`: dangling（どこからも参照されない）だけでなく、未使用のビルドキャッシュまで対象にする
- `-f`: 確認入力を省略する

これはイメージとVolumeを削除しないため、今回の`game-db-data`には触れない。対して`docker system prune -a`は未使用イメージまで削除する。`docker compose down`済みならPostgreSQLやRedisのイメージも未使用扱いになり、次回に再取得が必要になる。

`docker compose down -v`や`docker volume prune`はDBデータを消す可能性があるため、意図して初期化したい場合以外は実行しない。8 GiBはDockerでDBも同居させるには余裕が小さいため、恒久対応としてはEBSを16 GiB以上へ拡張するか、将来DBをマネージドサービスへ移すことを検討する。
