# ゲームAPIの入力検証・テストデータの基本メモ

## クライアント入力を信用しない

通常の`POST /players`では、クライアントが送れるのは`Name`と`Level`だけ。

```json
{
  "name": "Taro",
  "level": 1
}
```

初期Goldはサーバーが`100`へ固定する。クライアントがGoldを自由に指定できると、ゲーム内通貨を不正に作れてしまう。

```text
クライアント → Name / Level
API → 入力検証
API → Gold = 100 をサーバーが決定
PostgreSQLへ保存
```

`[Required]`、`[StringLength]`、`[Range]`のData Annotationsと`AddValidation()`により、範囲外や未入力は400として返す。

## 通常APIとテスト用APIを分ける

負荷試験などではGold指定のPlayerが必要になる。そのため通常の`POST /players`を緩めるのではなく、開発専用の別APIを使う。

```text
POST /dev/test-players
```

テスト用APIは次の条件でのみ使える。

```text
ENABLE_TEST_DATA_API=true
かつ
X-Test-Api-Key が TEST_API_KEY と一致
```

- 機能フラグが`false`ならルート未登録。Swaggerにも出ず404
- `true`でキーが空なら、アプリ起動時にエラー
- キー不一致なら403

テスト終了後は`.env`の`ENABLE_TEST_DATA_API=false`へ戻し、`docker compose up --build -d`で再起動する。

## APIキーの役割

`TEST_API_KEY`は「開発者しか知らない値」をEC2の`.env`へ置く。リクエストの`X-Test-Api-Key`と完全一致するかで許可を決める。

```text
.envの TEST_API_KEY
        と
HTTPヘッダーの X-Test-Api-Key
        を比較
```

`.env`を書き換えられる人はEC2へログインできる人なので、このキーはサーバー管理者自身を防ぐものではない。外部API利用者がテスト用機能を使うのを防ぐ境界。
