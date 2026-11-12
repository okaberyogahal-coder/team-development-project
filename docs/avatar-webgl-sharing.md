# V3 WebGLビルドの共有（Git LFS）

対象ブランチ：`feature/unity-avatar-futurecity`。メンバー提供の
`Miraifu_Avatar_WebGL_v1.zip` に含まれる次の2つのフォルダを共有する。

- `public/unity/AvatarV3Unified20261104/`：アバター編集用
- `public/unity/AvatarV3World20261104/`：街用

ZIPの外側のフォルダや `__MACOSX` は配置しない。
`.data` と `.wasm` は `.gitattributes` によりGit LFSで管理する。
HTML・JavaScript・release.jsonは通常のGitで管理する。
街の `.data` は100 MiBを超えるため、通常Gitでの追加は禁止。
`FutureCity` や旧プレビューのビルド除外方針は変更しない。

## メンバーが初めて取得するとき

Git LFSをインストールする。MacでHomebrewを使用する場合：

```sh
brew install git-lfs
```

WindowsではGit LFSの公式インストーラーを利用できる。
https://git-lfs.com/

リポジトリのフォルダで次を実行する（LFSはこのリポジトリだけに設定）：

```sh
git lfs install --local
git pull
git lfs pull
git lfs fsck
```

この変更が取り込まれたブランチを取得すること。取り込み前の
`feature/unity-avatar-futurecity` にはビルド本体がない。
通常のGitHubのZIPダウンロードではなく、Git＋LFSで取得する。
取得後は両フォルダの `Build` に `.data`・`.wasm`・`.loader.js`・
`.framework.js` が揃い、`.data` が短いLFSポインター文書ではなく
実際のバイナリであることを確認する。

## ビルドを更新するとき

同じリリースのHTML・bridge.js・Build・release.jsonをまとめて更新する。
`release.json` は提供元の情報を保持する。別のビルドのバイナリと混ぜない。

```sh
git add .gitattributes .gitignore docs/avatar-webgl-sharing.md public/unity/AvatarV3Unified20261104 public/unity/AvatarV3World20261104
git lfs status
git lfs fsck
```

`.data` と `.wasm` がLFS対象になっていることを確認してからコミット・プッシュする。
LFSのpre-pushフックがバイナリをアップロードする。LFS転送をスキップしない。
GitHub側のLFS容量・転送量の上限や権限によってはアップロード・取得が失敗する。
CIやデプロイ環境でもLFSの取得を有効にし、ビルド前に `git lfs pull` を行う。

## 対応範囲

この変更はビルドファイルの取得・配置を解決するものであり、
ワールドの切替やUI、アバター保存、AIなどの動作を変更しない。
他のブランチには異なる接続方式のプレビューがあるため、
別ブランチへ取り込む場合はHTML・bridge.jsとの互換性を確認する。
