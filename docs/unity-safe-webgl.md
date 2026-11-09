# 既存V3資産だけを使うWebGLビルド

## 前提

Unity Hubで **6000.4.2f1** と **Web Build Support** を導入してください。新しいCloneで作業し、動作確認済みプロジェクトや未Commit変更のあるフォルダを使い回さないでください。

この修正では3つの自動生成登録を `MIRAIFU_ENABLE_LEGACY_AUTOGENERATION` 定義の内側に隔離しています。通常はこの定義を追加しないでください。初回起動だけでアバター・Scene・画像生成や旧WebGLビルドを始めることはありません。

**「★ v3 WebGLプレイヤーのビルド」やScene生成メニューは使わないでください。**

## 安全なビルド

Unityのコンパイル完了後、Playを停止し、未保存Sceneがない状態で
`Tools > Miraifu > Safe WebGL > Build existing Preview and World` を選択してください。
リポジトリ外の保存先を指定します。既存出力は上書きしません。

既存の以下2SceneのみをBuildPipelineへ渡します。Scene・Model・Prefab・Mesh・Appearance・Materialを生成・保存する処理は呼びません。ビルド前後に全AssetsのSHA-256を比較し、差分が出れば停止します。

- `Assets/MiraifuAvatarRuntimePrototype/Generated/v3-web/AvatarV3WebPreview.unity`
- `Assets/MiraifuAvatarRuntimePrototype/Generated/v3-world/AvatarV3World.unity`

CLI例（macOS）:

```sh
MIRAIFU_SAFE_WEBGL_OUTPUT="$HOME/Documents/MiraifuBuilds/run-01" \
"/Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit -buildTarget WebGL \
  -projectPath "$PWD/unity/FutureCity" \
  -executeMethod MiraifuSafeWebGLBuild.BuildBoth \
  -logFile "$HOME/Documents/MiraifuBuilds/run-01.log"
```

出力先に `AvatarV3Unified20261104`（Preview）、`AvatarV3World20261104`（World）が作られます。ビルド設定の圧縮・テンプレート・バックグラウンド実行は処理中のみ設定し、finallyで元に戻します。

## Webで確認

成果物の各 `Build` フォルダを、**新しいWeb検証Clone**の対応する `public/unity/<同名>/Build` に配置してください。既存ビルドのある作業フォルダには上書きしないでください。
Web側の依存関係・DB等は既存のセットアップ手順に従って起動し、`/avatar` を開きます（`/avatar/v3` も同じV3資産を使用します）。6体の切替と髪・服・色・表情を編集し、街へ入って選択内容が維持されることを確認してください。キャンバスにフォーカスしてWASD移動・Spaceジャンプを確認します。iframeとUnityのbridge.jsは同じoriginで配信してください。file://では起動できません。

初回インポートの既知フォルダ.meta追加20件・孤立unitypackage.meta削除2件はPRへ含めないでください。その他の未知の資産差分があれば停止してください。ShaderGraphの一時的なGUIDエラーはAPI Updater後の最終コンパイル結果で判定します。

## 共有

生成物はGitへ追加しません。メンバーは同じUnity版で上記手順からビルドできます。配布が必要なら出力2フォルダをZIP化し、SHA-256・ソースCommit・Unity版を添えてGitHub Releasesの別添付、またはアクセス制限された共有ストレージへ置いてください。ログ・認証情報・Libraryは含めません。WebGL一式を同じ階層で展開し、HTTP/HTTPSで配信します。公開アップロードは別途承認してから実施してください。

## WebGLInputの参照エラー

Unity 6000.4の公式APIでは `UnityEngine.WebGLInput.captureAllKeyboardInput` が有効です。
https://docs.unity3d.com/6000.4/Documentation/ScriptReference/WebGLInput.html

このブランチの基点から作ったクリーンCloneでは、`-buildTarget WebGL` によるPlayerコンパイルが成功し、コンパイラの応答ファイルにWebGL Supportの `Managed/UnityEngine.WebGLModule.dll` 参照が含まれることを確認しています。APIを削除したり、別namespaceへの置換や反射によってエラーを隠す変更はしていません。

CS0103が残る場合は、Unityの完全な版・Web Build Support導入・Web対象でのビルド・asmdefの `noEngineReferences: false` を確認してください。WebGLInputはEditorコンパイルでは条件分岐で除外されるため、Editor成功だけでは判定できません。既存の検証済みフォルダでLibrary削除や設定変更を試さず、独立Cloneで再現ログとPlayer用FutureCity.rspを保存してください。

## 動作確認結果

Unity 6000.4.2f1でPreview・Worldともビルド成功、全Assetsのビルド前後ハッシュ一致を確認しました。成功済み成果物を再利用し、Chromeの `/avatar` で6体を選択、代表例として髪型・表情・服装・色を変更し、`/plaza` のUnityに全設定が引き継がれることを確認しました。W/A/S/Dそれぞれの位置変化とSpaceによる上昇も確認し、ブラウザ例外は0件でした。ログイン・ネットワーク同期や全パーツ組合せの総当たりは今回の検証範囲外です。
