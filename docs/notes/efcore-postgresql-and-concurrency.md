# EF Core・PostgreSQL・同時実行の基本メモ

## Migrationの役割

Migrationは、C#のモデル変更をPostgreSQLのテーブル・列・インデックス変更として記録する差分ファイル。

```text
Models / GameDbContextを変更
  ↓ ローカルSDKでMigrationを作成
dotnet ef migrations add 名前
  ↓ Gitへコミット
EC2のAPI起動
  ↓ MigrateAsync()
PostgreSQLへ反映
```

`__EFMigrationsHistory`テーブルには、反映済みMigrationが記録される。

```text
No migrations were applied. The database is already up to date.
```

は、Migration失敗ではなく「反映する差分がない」という正常メッセージ。

## Transaction

購入では次の処理を同じTransactionに入れる。

```text
Goldを減らす
所持品を追加する
購入履歴を追加する
  ↓
全部成功したらCommit
どれか失敗したらRollback
```

途中で失敗してもGoldだけ減る状態を防ぐ。

## 条件付きUPDATEと同時購入

```sql
UPDATE "Players"
SET "Gold" = "Gold" - @price
WHERE "Id" = @playerId
  AND "Gold" >= @price;
```

残高確認と減算を1つのUPDATEで行う。更新件数が1なら購入成功、0ならGold不足またはPlayer不存在。

```text
Gold 100、価格70で同時購入

1件目: 100 → 30 で更新成功
2件目: 1件目の更新後に条件を再確認し、30 >= 70ではないため0件更新
```

PostgreSQLだけの特殊機能ではなく、SQL Serverでも同じ条件付きUPDATEの考え方を使える。アプリ側で`SELECT → if → UPDATE`と分けると、複数リクエストが同じ古いGoldを読めるため危険。

## デイリー報酬の二重受取防止

`PlayerRewardClaims`には次のユニーク制約がある。

```text
PlayerId + RewardCode + RewardDate
```

`RewardDate`はゲームサーバーのJST日付。`ClaimedAt`は監査用のUTC時刻。

```text
Player 1 / daily-login / 2026-08-12
```

を2回登録しようとしても、PostgreSQLがユニーク制約違反として拒否する。`ClaimedAt`だけでは「同じゲーム日か」を表しにくいため、業務上の日付は別に持つ。

## ランキング用インデックス

```text
Gold DESC, Level DESC, Id ASC
```

は、ランキングの`ORDER BY Gold DESC, Level DESC, Id ASC LIMIT ...`に合わせた複合インデックス。少人数では差が見えにくいが、件数が増えたときの並び替え負荷を抑える土台になる。
