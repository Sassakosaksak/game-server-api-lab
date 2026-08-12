# Game Server API Lab

ASP.NET Core、PostgreSQL、Redis、Docker Composeを使ったゲームサーバーAPIの学習用プロジェクトです。

## 実装済み機能

- プレイヤー作成・一覧・詳細取得
- 開発用JWTログインと認証済みプレイヤー情報取得
- デイリーログイン報酬と、DBの一意制約による二重受取防止
- Goldを使ったショップアイテム購入とTransaction処理
- Redisによるプレイヤー情報・ランキングのキャッシュ
- Gold、Level、ID順のプレイヤーランキング
- JSON形式の構造化ログ
- mainへのpushとPull Requestで実行するGitHub ActionsのReleaseビルド
- k6によるランキングAPIの負荷試験

## 構成

```text
Swagger / クライアント
        |
        v
ASP.NET Core API
   |           |
   v           v
PostgreSQL    Redis

k6 -> API（ランキングAPIの負荷試験時のみ）
```

Docker Composeでは、API、PostgreSQL、Redisを起動します。k6はテスト専用profileのため、通常起動では実行されません。

## 必要な環境

- Docker Engine と Docker Compose
- ローカルで.NETから実行する場合は .NET 10 SDK

## 起動方法

### 1. `.env` を作成する

`.env.example`をコピーして、Git管理しない`.env`を作成します。

```bash
cp .env.example .env
```

Windows PowerShellでは次を使います。

```powershell
Copy-Item .env.example .env
```

`.env`の`POSTGRES_PASSWORD`と`JWT_KEY`は、必ず十分に長いランダムな値へ変更してください。`JWT_KEY`は32バイト以上必要です。

開発専用テストデータAPIは、既定では無効です。必要な場合だけ次のように設定します。

```dotenv
ENABLE_TEST_DATA_API=true
TEST_API_KEY=十分に長いランダムな値
```

確認後は、`ENABLE_TEST_DATA_API=false`へ戻してください。

### 2. コンテナを起動する

```bash
docker compose up --build -d
```

- `--build`: APIのDockerイメージを作り直す
- `-d`: 起動後にバックグラウンドで動かす

起動状態を確認します。

```bash
docker compose ps
```

`api`が`running`、`redis`が`healthy`なら起動成功です。

### 3. Swaggerを開く

ローカルDockerでは、次をブラウザで開きます。

```text
http://localhost:8080/swagger
```

EC2では、セキュリティグループで8080番ポートを許可したうえで、次の形式で開きます。

```text
http://<EC2のパブリックIP>:8080/swagger
```

## Swaggerでの確認順序

1. `POST /players`でプレイヤーを作成する。
2. `POST /auth/dev-login`へ作成した`playerId`を送信し、`accessToken`を取得する。
3. Swagger右上の`Authorize`を開き、取得した`accessToken`だけを入力してJWTを設定する。
4. `GET /players/me`、`POST /rewards/daily-login/claim`、`POST /shop/items/{itemCode}/purchase`を実行する。
5. `GET /rankings/players?top=10`でランキングを確認する。

`GET /players/me`と`GET /rankings/players`のレスポンスヘッダーには、Redisキャッシュの状態が`X-Cache: MISS`または`X-Cache: HIT`として出力されます。

## ログ確認

APIのJSON形式ログを継続表示します。

```bash
docker compose logs -f api
```

`TraceId`、HTTPメソッド、パス、ステータスコード、処理時間が記録されます。JWTやテストAPIキーはログに出力しません。

## 負荷試験

ランキング取得APIだけを対象に、k6で読み取り負荷試験を実行できます。

```bash
docker compose run --rm -e VUS=100 -e DURATION=1m k6
```

- `VUS`: 最大仮想ユーザー数
- `DURATION`: 最大VUを維持する時間
- `--rm`: 試験終了後にk6コンテナを削除

実施済みの10 / 100 / 500 / 1000 VU試験の条件と結果は、[負荷試験結果](docs/load-test-results.md)を参照してください。

## CI

`.github/workflows/build.yml`により、mainブランチへのpushとmain向けPull Requestで、次を自動実行します。

```text
NuGetパッケージ復元
→ Release構成でのビルド
```

## 今後の候補

- 任意: WebSocketによるリアルタイム通信
