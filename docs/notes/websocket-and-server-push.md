# WebSocket・サーバーからの通知の基本メモ

## HTTPとWebSocketの役割

通常のHTTP APIは、クライアントがリクエストを送り、サーバーが応答して終わる。

```text
ブラウザ → GET /rankings/players → API → 応答 → 接続終了
```

WebSocketは最初に接続を確立した後、接続を維持する。サーバー側から任意のタイミングで通知を送れる。

```text
ブラウザ → WebSocket接続 /ws/rankings
ブラウザ ← 接続を維持したまま通知を受信
```

非同期処理は、サーバー内部でDBやネットワークの応答待ちを効率よく扱うプログラミング上の仕組み。WebSocketはブラウザとサーバー間で双方向通信を行うための通信プロトコルであり、両者は別の概念である。

## 今回のランキング通知

```text
1. ブラウザがGET /rankings/playersで最初のランキングを取得
2. ブラウザが/ws/rankingsへWebSocket接続を維持
3. 誰かがプレイヤー作成・報酬受取・購入を成功させる
4. APIが接続中クライアントへ {"type":"rankings-updated"} を送信
5. ブラウザが通知を受ける
6. ブラウザ側のJavaScriptがランキングAPIを再取得し、画面を更新できる
```

現在実装済みなのは3〜4のサーバー側と、ブラウザConsoleでの5の確認まで。6の画面再描画は、将来HTML内のJavaScriptやReactなどのフロントエンドで実装する。

## ブラウザでの確認

Swaggerを開いたブラウザの開発者ツールConsoleで、次を実行する。

```javascript
const rankingSocket = new WebSocket(`ws://${location.host}/ws/rankings`);

rankingSocket.onopen = () => console.log('WebSocket 接続成功');
rankingSocket.onmessage = (event) => console.log('ランキング通知:', event.data);
```

`location.host`はSwaggerを開いているホスト名とポート番号。HTTPでSwaggerを開いている現在は`ws://`を使う。将来HTTPS化した場合は、暗号化されたWebSocketである`wss://`を使う。

Consoleに接続成功が出たままSwaggerで`POST /players`を実行し、`ランキング通知: {"type":"rankings-updated"}`が出れば、接続・サーバー通知・ブラウザ受信が確認できる。

## 現時点の制約

接続中ソケットの一覧はAPIプロセスのメモリ内に保持している。そのためAPIを複数台に増やすと、更新したAPI以外に接続しているクライアントには通知できない。複数台構成ではRedis Pub/SubなどでAPI間にも通知を配信する設計が必要になる。
