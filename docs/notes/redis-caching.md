# Redisキャッシュの基本メモ

## Redisを正本にしない

PlayerのGold、購入履歴、報酬受取済み判定の正本はPostgreSQL。Redisは読み取りを速くする補助であり、消えてもDBから復元できるデータだけを置く。

```text
購入可否・二重受取判定 → PostgreSQL
GET /players/meの表示用データ → Redisにキャッシュしてよい
```

## Cache-Aside

`GET /players/me`はCache-Aside方式。

```text
1. Redisからプロフィールを読む
2. あればRedisの値を返す（HIT）
3. なければPostgreSQLから読む（MISS）
4. DBの値をRedisへ保存して返す
```

Redis障害時はWarningログを残し、PostgreSQLから返す。

## プレイヤーキャッシュの世代番号

```text
player:1:cache-version
  → 2e1a0267469d47fe8d01f1e04f64e053

player:1:profile:2e1a0267469d47fe8d01f1e04f64e053
  → Player 1のJSON
```

取得時は先に`cache-version`の値を読み、その値を含むプロフィールキーを組み立てる。

```text
GET player:1:cache-version
GET player:1:profile:{取得した世代}
```

購入・報酬後は、`cache-version`だけを新しいGUIDへ変える。古いプロフィールキーは残っていても参照されず、TTLで消える。

```text
古い世代のキーを前方一致で全削除しない
  ↓
新しい世代へ参照先を切り替える
```

GUIDの`ToString("N")`は、ハイフンなしの16進数32文字。Base64ではない。

## TTLとキー走査

- プロフィールキャッシュ: 5分
- ランキングキャッシュ: 1分
- 世代キー: 1日

`KEYS player:*`はRedis全体を一気に走査するため、本番の通常リクエストでは使わない。調査やバッチなら`SCAN`を少しずつ行う。

```bash
docker exec game-redis redis-cli --scan --pattern 'player:*'
```

`--scan`はキーの確認用。アプリの購入処理で前方一致削除を行うためのものではない。

## ランキングキャッシュとZSET

今のランキングは、PostgreSQLで並べた結果をRedisへ短時間キャッシュする方式。

Redis ZSETは別の選択肢で、`member=Player ID`、`score=Gold`として順位順をRedis自身に維持するデータ型。

```text
ZADD ranking:players:gold 200 "2"
```

ならPlayer 2のGoldだけを更新し、Redis内の順位を動かせる。更新頻度が高いリアルタイムランキング向けだが、DBとの整合性・再構築・同点時の細かい順位ルールを別途設計する必要がある。
