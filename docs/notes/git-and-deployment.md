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

## GitHub Actionsのビルド

`.github/workflows/build.yml`は、次の2つでビルドを実行する。

```text
mainへのpush
マージ先（base branch）がmainのPull Requestの作成・更新
```

Pull Requestでは、mainへ取り込む前に「PR元ブランチの現在のコード」をGitHubの一時的なUbuntu環境でビルドする。PRへ追加pushした場合も更新として再実行される。mainへマージされるとmainへのpushになるため、mainのコードでも再度ビルドされる。

```yaml
permissions:
  contents: read
```

これはActionsにリポジトリのファイルを読む権限だけを与える指定。push、Issue操作、Secrets変更、EC2操作の権限は与えない。

```text
dotnet restore
  → NuGetパッケージをダウンロードし、依存関係を復元する
dotnet build --configuration Release --no-restore
  → 復元済みの依存関係を使って、Release構成でコンパイルする
```

`--no-restore`は、直前の`dotnet restore`をもう一度実行しない指定。NuGetの復元を省くだけで、Redisなどのコンテナを起動する処理ではない。

DebugとReleaseはビルド構成。Debugは開発時のデバッグ情報を重視し、Releaseは配布・実行向けに最適化する。現在のDockerfileとGitHub Actionsでは、明示的にRelease構成を使っている。
