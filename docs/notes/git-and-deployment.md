# Git・GitHub・デプロイの基本メモ

## ローカルからGitHubへ

通常の流れは次のとおり。

```text
コード変更
  ↓
dotnet build
  ↓
git add
git commit -m "feat: 日本語の変更内容"
  ↓
git push origin main
```

秘密値入りの`.env`、`.pem`鍵、`appsettings.Local.json`はGitへ入れない。

## GitHubからEC2へ

EC2のプロジェクトフォルダで実行する。

```bash
cd ~/GameServerApi
git pull
docker compose up --build -d
```

- `git pull`: GitHubの最新コミットをEC2へ取得して取り込む
- `docker compose up --build -d`: 変更済みAPIイメージを作り直して、裏で起動する

通常は`scp`で個別ファイルを送るより、GitHubへpushしてEC2で`git pull`する方が、変更履歴と反映内容を追いやすい。

## upstreamがないときの`git pull`

初回などで次のように出ることがある。

```text
There is no tracking information for the current branch.
```

ローカル`main`とGitHubの`origin/main`の追跡関係が未設定という意味。

```bash
git branch --set-upstream-to=origin/main main
```

を一度実行すると、以後は`git pull`だけでよい。

## 変更単位とコミット

「初期Goldをサーバー固定」と「テスト専用API追加」のように、目的が違う変更はコミットも分ける。どの変更を戻す・確認するかが分かりやすくなる。
