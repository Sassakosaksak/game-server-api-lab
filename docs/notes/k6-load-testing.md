# k6負荷試験の基本メモ

## k6を使う理由

k6はJavaScriptで試験シナリオを書けるCLI型の負荷試験ツール。JMeterはGUIで試験を組めるため手軽だが、k6はスクリプトと実行条件をGit管理しやすく、Docker上で同じ試験を再実行しやすい。今回のようにEC2内でコマンド実行する用途と相性がよい。

## Compose profileと`run`

`k6`サービスには`profiles: ["load-test"]`がある。通常の`docker compose up -d`ではk6は起動しないため、普段のAPI運用に負荷試験が混ざらない。

```bash
docker compose run --rm -e VUS=100 -e DURATION=1m k6
```

- `run`: 指定したサービスだけを一時コンテナとして実行する
- `k6`: Composeのサービス名。ログの検索条件ではない
- `--rm`: 試験終了後に一時k6コンテナを削除する
- `-e`: コンテナへ環境変数を渡す

`run`でサービスを明示指定する場合は、profileの通常起動対象外であってもk6を実行できる。`docker compose --profile load-test up`なら、通常サービスに加えてprofile対象のk6も起動対象になるが、k6は試験終了後に停止するため、今回の用途では`run --rm`が合う。

## スクリプトの流れ

```text
VUSとDURATIONを環境変数から受け取る
  → 30秒かけてVUを増やす
  → DURATIONだけ最大VUを維持する
  → 30秒かけて0まで減らす
  → 各VUはランキングGET後に1秒待つ
```

`export const options`は、k6が読み取る試験設定を外へ公開するための書き方。`export default function`は、各仮想ユーザーが繰り返し実行する本体である。

`response`と`result`は同じ種類のHTTPレスポンスを指す変数名。前者はGETの戻り値、後者は`check`内の関数が受け取る引数であり、名前が違うだけで同じレスポンスオブジェクトを参照している。

`sleep(1)`がないと、各VUが応答直後に次のリクエストを送る。これは通常ユーザーの操作間隔とかけ離れ、意図せず非常に強い負荷と大量ログを発生させる。

## 結果の読み方

```text
http_req_failed: ['rate<0.01']  → 失敗率1%未満
http_req_duration: ['p(95)<500'] → 95パーセンタイルが500ms未満
checks: ['rate>0.99']            → 独自チェック成功率99%超
```

`rate<0.01`などはk6のthreshold構文。閾値を一つでも超えると、試験全体は失敗扱いになる。

`p(95)=3.08ms`は、遅い方から5%を除いた95%のリクエストが3.08ms以内だったという意味。最大値だけが558msでも、1回程度の遅いリクエストならp95が低いままになる。500 VUではp95が1.61秒となり閾値を超え、1000 VUではタイムアウトも発生した。詳細は[負荷試験結果](../load-test-results.md)を参照する。

`docker stats`は実行中のCPU・メモリ・ネットワーク量を継続表示する。`docker stats --no-stream`は、そのコマンドを打った瞬間の1回だけを表示する。負荷試験中は別ターミナルで`docker stats`と`docker compose logs -f api`を確認し、CPU・メモリ枯渇、Errorログ、タイムアウトを観察する。

## 結果ファイル

現在のCompose定義では、k6結果は標準出力だけで、ファイルには保存していない。k6は`--summary-export=/results/summary.json`で集計結果ファイルを出せる。EC2上にも残すなら、`./load-test-results:/results`のようなVolumeと、Git管理しない出力先を別途設計する。
