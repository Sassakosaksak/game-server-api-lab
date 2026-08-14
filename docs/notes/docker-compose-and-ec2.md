# Docker Compose と EC2 の基本メモ

## このプロジェクトのコンテナ構成

```text
インターネット
  → EC2の8080番ポート
  → game-api（ASP.NET Core）
      ├─ game-db（PostgreSQL）
      └─ game-redis（Redis）
```

外部へ公開するのはAPIの`8080`だけ。PostgreSQLとRedisはComposeネットワーク内でAPIからだけ接続する。

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

## コンテナにSDKがない理由

最終APIイメージは実行専用の.NET Runtimeであり、SDKを含まない。そのためコンテナ内で`dotnet ef ...`を実行すると「SDKがない」と失敗する。

Migrationの作成はローカルSDKで行い、EC2ではアプリ起動時の`MigrateAsync()`が反映する。
