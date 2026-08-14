# JWT・Swagger・HTTPヘッダーの基本メモ

## JWTの役割

JWTは、ログイン済みPlayerを示す署名付きトークン。開発用ログインAPIはPlayer IDを基にJWTを発行し、本人用APIはJWTの`sub`クレームからPlayer IDを取り出す。

```text
POST /auth/dev-login
  ↓ JWTを受け取る
GET /players/me
  Authorization: Bearer <JWT>
  ↓
JWTのsub → Player ID
```

JWTは署名キー、Issuer、Audience、有効期限を検証する。JWTキーを変えると、過去に発行したJWTは検証できなくなる。

Swaggerの`Authorize`入力はブラウザを再読み込み・再デプロイすると消えることがある。JWTそのものが有効かどうかとは別の、Swagger UI上の入力状態である。

## HTTPリクエストの値の置き場

```text
POST /dev/test-players?providedTestApiKey=abc  ← クエリ

X-Test-Api-Key: abc                            ← ヘッダー

{ "name": "Taro", "gold": 100 }             ← JSONボディ
```

- クエリ: URLに付ける検索条件など
- ヘッダー: JWT、APIキーなど、リクエストの認証情報
- ボディ: 作成・更新するデータ

APIキーをクエリに載せるとURL、アクセスログ、履歴へ残りやすいため、通常はヘッダーに載せる。

## `[FromHeader]`

```csharp
[FromHeader(Name = "X-Test-Api-Key")] string? providedTestApiKey
```

これはASP.NET Coreへ「`providedTestApiKey`はHTTPヘッダーの`X-Test-Api-Key`から受け取る」と指定する属性。

```text
Swaggerで値を入力してExecute
  ↓ SwaggerがHTTPヘッダーを付けて送信
X-Test-Api-Key: 入力値
  ↓ ASP.NET Core
providedTestApiKey変数へ代入
  ↓ .env由来のTEST_API_KEYと比較
一致なら許可、不一致なら403
```

`[FromHeader]`はサーバー側の受け取り場所指定。SwaggerはAPI仕様を読んでヘッダー入力欄を表示・送信する。ゲームクライアントや`curl`では、クライアント側がヘッダーを付ける実装を行う。

```csharp
string? providedTestApiKey
```

だけの場合は、通常クエリの`?providedTestApiKey=...`から受け取る。キーが一致すれば処理は通るが、URLへ秘密値を載せる設計になる。

## `AllowAnonymous()`

`AllowAnonymous()`は「JWT認証を要求しない」というエンドポイント指定。テストAPIはJWTの代わりに`X-Test-Api-Key`を検証するために使っている。

現在のプロジェクトは全APIにJWTを強制する既定ポリシーを設定していないため、技術的には省略しても同じ動作になる。意図を示す意味を持つ。
