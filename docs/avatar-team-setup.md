# チーム向けアバターメーカーの再現手順

対象ブランチは `feature/unity-avatar-futurecity`。WebのソースとUnityのソースはGitにありますが、WebGLの `Build` はGit対象外です。**Cloneだけでは3D表示できません**。以下の依存関係セットアップと、別途配布されたPreview・World成果物の配置が必要です。

## 1. 独立したCloneとWeb依存関係

Node.jsは `package.json` のenginesに合う版（22.12以上の22系、または24以上）を使います。

```sh
git clone --single-branch --branch feature/unity-avatar-futurecity https://github.com/okaberyogahal-coder/team-development-project.git avatar-web
cd avatar-web
npm ci
npm run db:generate
```

`npm ci` のpostinstallもPrisma Clientを生成します。`npm run db:generate` は明示的な生成・修復用です。`generated/prisma` はコミットしません。`Can't resolve '../../generated/prisma/client'` はこの生成が未完了の状態です。

未ログインでアバター編集・ローカルのUnityワールドを確認するだけならDB・認証設定は不要です。ログイン画面やアカウント保存、複数人同期は別の検証です。[認証手順](authentication.md)・[DB手順](supabase.md)を参照してください。認証を使う場合は `BETTER_AUTH_URL=http://localhost:5000` とし、各自の秘密値をローカルで設定します。他人の `.env` やDBをコピーしないでください。

## 2. WebGL成果物を配置する（通常のメンバー向け）

管理者から、同じソースCommitとUnity版に対応した成果物2組とSHA-256一覧を受け取ります。歩行修正はPR #16（コミット `b8b66d2`、マージ `842f920`）を含む成果物を使います。Git上の歩行ソースだけ更新しても、古いWebGLバイナリの動作は変わりません。

リポジトリ内の `index.html` と `bridge.js` はそのまま使い、受け取った **Buildフォルダだけ**を以下へコピーします。

```text
public/unity/AvatarV3Unified20261104/Build/
  AvatarV3Unified20261104.loader.js
  AvatarV3Unified20261104.framework.js
  AvatarV3Unified20261104.data
  AvatarV3Unified20261104.wasm
public/unity/AvatarV3World20261104/Build/
  AvatarV3World20261104.loader.js
  AvatarV3World20261104.framework.js
  AvatarV3World20261104.data
  AvatarV3World20261104.wasm
```

macOS/Linuxのコピー例（配布物を展開した親フォルダを指定）:

```sh
export AVATAR_WEBGL_SOURCE=/absolute/path/to/distributed-artifacts
python3 - <<'PY'
import os, shutil
from pathlib import Path
source = Path(os.environ['AVATAR_WEBGL_SOURCE'])
names = ['AvatarV3Unified20261104', 'AvatarV3World20261104']
# 両方を事前確認し、既存の配置先があればコピー前に停止する。
for name in names:
    target = Path('public/unity') / name / 'Build'
    if target.exists():
        raise SystemExit(f'STOP: existing destination: {target}')
    for ext in ['loader.js', 'framework.js', 'data', 'wasm']:
        if not (source / name / 'Build' / f'{name}.{ext}').is_file():
            raise SystemExit(f'STOP: missing source: {name}.{ext}')
for name in names:
    shutil.copytree(source / name / 'Build', Path('public/unity') / name / 'Build')
PY
```

受領時と配置後に配布元のSHA-256一覧で照合してください。Build・Prisma生成物・node_modules・`.env` をGitに追加しないでください。`git status --short` で意図しない追跡ファイル変更がないことを確認します。

## 3. localhost:5000で起動

```sh
npm run dev -- --port 5000 --hostname localhost
```

5000番が使用中なら既存プロセスを終了せず停止します。macOSのAirPlayレシーバーが使っていることもあります。別の検証サーバーを同時起動する場合は空いている別ポートに置き換えます。3000番・3046番や既存の動作環境には触れません。

開くURL:

- 全6体: http://localhost:5000/avatar
- 女性3体（おばあちゃんを含む）: http://localhost:5000/avatar?avatarGenderFixture=female
- 男性3体: http://localhost:5000/avatar?avatarGenderFixture=male

両Buildの4ファイルがHTTP 200になることを確認します。初回は約100MBのdataと約48MBのwasmをそれぞれ読み込むため、3D起動を待ちます。file://では起動できません。

STEP 1で6体を表示・選択できることを確認し、代表1体でSTEP 2の髪型・表情・服・色を変更します。「このアバターで街へ行く」からSTEP 3へ進み、同じ外見が引き継がれることを確認します。Unity画面をクリックしてWを10秒以上保持し、足が動き続けること、キーを離すと停止すること、A/S/D移動とSpaceジャンプ・着地を確認してください。

## 4. 成果物の配布と再ビルドの区別

対応する配布物を受け取れるメンバーはUnityのインストール・再ビルド不要です。配布物がない、またはUnityランタイムのソースが変わった場合のみ、担当者が独立したUnity用Cloneで[安全なWebGLビルド手順](unity-safe-webgl.md)を実施します。完成済みアバター・Sceneの再生成や旧ビルドメニューは使いません。

推奨配布は、2つのBuildフォルダを含むZIPとSHA-256一覧・ソースCommit・Unity版・ビルド日時を、アクセス範囲を確認したGitHub Releasesまたはチーム共有ストレージに置く方法です。通常のGitコミットへバイナリを追加しません。ソース一式・Library・ログ・認証情報はZIPに含めません。この手順書の追加は成果物の公開アップロードを意味しません。

## 2026-11-09 新規Cloneでの検証記録

GitHubから取得した `842f920e7c041ad2af5ba7ba3c93332a52799280` で確認しました。既存の5000番環境を変更しないため、新規Cloneは同じ起動オプションのポートだけを5001へ変更しています。新規Cloneを5000番で起動した検証ではありません。

- Node.js 22.17.0。`npm ci` と `npm run db:generate` 成功。既存環境のnode_modules・Prisma Client・.envはコピーしていません。
- WebGLはGitHub外の保存済み `unity-walk-live-v16-output` から両Buildを配置。ビルド来歴はsourceCommit `842f920`、includesPr16=true。8ファイルのコピー元・先のSHA-256一致を確認。Unity再ビルドは実施していません。
- `/avatar` HTTP 200、両Buildのloader.js/framework.js/data/wasmの計8ファイルもHTTP 200。
- STEP 1の6体の表示と各選択ボタンの選択状態、Unity 3Dプレビューの表示を確認。
- おばあちゃんでSTEP 2へ進み、ハイネックジャケット、サンセットオレンジ、すっきりショート、ゴールドブロンド、笑顔を選択。服色の3D反映を確認。
- STEP 3の `/plaza` へ遷移し、World iframeのURLに `baseAvatarId=elderly-woman`、`face=1`、`hair=1`、`outerwear=1`、`hairColor=#D4AF37`、`topColor=#EA580C` が含まれることを確認。
- WorldのUnity起動ロゴ表示後、ブラウザ自動操作の接続が失われたため、街での外見反映・10秒連続歩行・停止・ジャンプは今回未確認です。過去の結果を今回の成功として扱っていません。配布前に代表1体でこの残りの確認を実施してください。
- npm auditはmoderate 9件を報告しました。この手順追加では依存関係の更新・audit fixは実施していません。

したがって、今回の結果は「GitHubの新規Clone + npmセットアップ + 別配布WebGLで編集とWorldへの設定転送まで再現」です。Clone単体での3D動作や、街での移動までの再現完了を意味しません。
