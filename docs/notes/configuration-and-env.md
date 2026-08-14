# 設定・`.env`・環境変数の基本メモ

## 設定が届く流れ

Docker Composeで動かす場合、設定の流れは次のとおり。

```text
EC2の .env
  ↓ Composeが ${変数名} を展開
docker-compose.yml の environment:
  ↓ APIコンテナの環境変数
ASP.NET Core設定
  ↓ appsettings.jsonを上書き
Program.cs
```

`.env`をASP.NET Coreが直接読むわけではない。Composeが`.env`を読み、`environment:`に書かれた値だけをコンテナへ渡す。

## `__` と `:`

```yaml
TestApi__Key: ${TEST_API_KEY:-}
```

コンテナ環境変数の`__`は、ASP.NET Coreでは設定階層の`:`として扱われる。

```text
TestApi__Key  →  TestApi:Key
```

そのためC#では次のように取得できる。

```csharp
var testApiKey = builder.Configuration["TestApi:Key"];
```

`Features`や`TestApi`はASP.NET Coreの予約語ではなく、設定を分類するためにアプリ側で決めたグループ名。

## `${VAR:-default}`

```yaml
Features__EnableTestDataApi: ${ENABLE_TEST_DATA_API:-false}
```

- `ENABLE_TEST_DATA_API`に値があれば使う
- 未設定または空文字なら`false`を使う

`:-`の後ろが既定値。`${TEST_API_KEY:-}`は既定値が空文字であることを表す。

## appsettingsと環境変数の優先順位

`appsettings.json`は基本値を置く場所。コンテナ環境変数の方が優先され、同じ設定キーを上書きする。

```json
"TestApi": { "Key": "" }
```

はDockerなしで起動する場合の既定値であり、EC2では`.env`由来の値で上書きされる。

## 秘密値の扱い

`.env`にはDBパスワード、JWTキー、テストAPIキーを置く。Git管理しないため`.gitignore`へ`.env`を入れる。

`.env.example`には値の形式だけを残す。実際の秘密値は書かない。
