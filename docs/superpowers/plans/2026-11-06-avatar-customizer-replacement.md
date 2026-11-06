# V3 Avatar Customizer Replacement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 他メンバー作成の男女6体のWeb着せ替えを `/avatar` へ搭載し、現在のワールドとマイページに本人の同じ外見を表示する。

**Architecture:** 受領コミットから必要なWeb UI・画像・V3描画資産だけを取り込む。旧appearanceと新appearanceを明示的に分け、現在の人物ルート・認証・街を維持する描画アダプターを接続する。保存は本人別のブラウザ保存を継続する。

**Tech Stack:** Next.js 16 / React / TypeScript、Unity 6000.4.2f1 / C# / WebGL、既存PostgreSQL・Better Auth、Playwright。

**Spec:** `docs/superpowers/specs/2026-11-06-avatar-customizer-replacement-design.md`（2026-11-06承認、男女6体の自由選択）。

## Global Constraints

- 受領元は `origin/feature/unity-avatar-futurecity` の `d71d307` に固定する。作業基点 `db18f8c` の街・本人確認・プロフィールを維持する。
- 通常ページは若者・大人・シニアの男女6体すべてから自由に選べる。登録された性別・年齢で候補を制限しない。
- 未ログインの `/avatar` はログインへ案内する。ワールドのゲスト見学は維持する。
- 新UIを開くだけで旧完成データを消したり、初期値で上書きしたりしない。
- DBマイグレーション・端末間同期は追加しない。DBを使う試験は専用ローカル `*_test` だけを使う。
- 受領元のV3ワールドへのルート変更、NPC全員のV3化、メール・パスワード変更を行わない。
- Next.jsの変更前に `node_modules/next/dist/docs` の該当ガイドを読む。

## Review Focus

- 別タブでログイン利用者が変わった時、前の利用者の編集中／完成済み衣装を保存・適用しない（Task 2/4/6）。
- 初回表示・StrictMode再実行・iframe再読み込みで、初期値や古いACKが完成済み外見を上書きしない（Task 2/4）。
- 表示候補が欠けるビルド・不正パーツ・プレビュー失敗では、完成扱いせず旧外見を維持する（Task 1/3/4）。
- マイページの閲覧だけで衣装やPlayPrefsが書き換わらず、登録性別の自動描画処理でV3選択が上書きされない（Task 3/5）。
- 街への移動・全画面・本人切り替えの後も、移動・カメラ・クリック・認証を担う既存オブジェクトを失わない（Task 3/6）。

---

## ファイル構成

- Web：受領 `app/avatar/AvatarCustomizer.tsx`・`avatar.css`、`shared/avatar-{types,candidates,catalog,thumbnails,development-assets}.ts`、`shared/avatar-parts.json`、参照する `public/images/avatar-*` を必要な範囲で取り込む。
- 保存境界：新規 `shared/avatar-v3.ts`・`shared/avatar-v3-storage.ts`。既存 `shared/demo-profile.ts`・`shared/account-avatar.ts` を新旧形式対応へ拡張する。
- Unity：新規 `Assets/MelifeAvatarV3/Runtime/` に受領描画コアを元のmeta/GUIDとともに配置する。新規 `FutureCity/Runtime/Mirai/V3AvatarAdapter.cs` と既存WebProfile・ImportedAvatar・PersonAnimatorを接続する。
- 出力：新規 `FutureCity/Editor/AvatarV3Build.cs`・`scripts/build-avatar-v3.mjs`。既存のFutureCity出力を維持し、着せ替え用プレビューだけを別出力にする。
- Web連携：`app/avatar/page.tsx`、`app/components/UnityExperience.tsx`、`AccountAvatarPreview.tsx`、Unityの `Melife/index.html`・既存ブリッジを更新する。

### Task 1: 受領資産の依存閉包とV3プレビューを再現する

**Files:** 新規 `scripts/import-avatar-v3.mjs`・`scripts/build-avatar-v3.mjs`、`unity/FutureCity/Assets/MelifeAvatarV3/`、`FutureCity/Editor/AvatarV3Build.cs`、参照画像。変更 `package.json`・`.gitignore`。試験 `tests/avatar-v3/assets.test.ts`、`FutureCity/Tests/Editor/V3AssetTests.cs`。

**Interfaces:** `import-avatar-v3.mjs --check` は固定コミットの依存・GUID・必要画像を照合して終了コードを返す。`AvatarV3Build.BuildPreview()` は `public/unity/AvatarV3Unified20261104` へプレビューを出力する。6体を取得する `V3AvatarLibrary` ScriptableObjectを `Resources/FutureCityAvatars/V3Library` に生成する。

- [ ] 6モデルが揃い、各モデルの必須パーツ・参照画像・GUIDが解決する検査を先に書き、未導入時に失敗することを確認する。
- [ ] `Generated/v3-web/AvatarV3WebPreview.unity`、6モデルのruntime/appearanceアセットを起点にmeta GUIDの参照を再帰解決する。既存と同一の依存は再利用し、衝突する別内容のGUIDは明示的に停止する。フォルダmetaも保持する。
- [ ] 必須コードを同じ独立runtime assemblyへ配置する：`MiraifuRuntimeAvatar`・`MiraifuRuntimeAvatarModel`・`MiraifuRuntimeAvatarLab`・`MiraifuWebAvatarReceiver`・`MiraifuAppearanceRuntime`・`MiraifuAppearanceSet`・`MiraifuFaceSeedWebBridge`。参照はUnityランタイムとInputSystem。FutureCity依存の受領MovementBridgeとEditor自動ビルド処理は入れない。
- [ ] 明示的なビルドコマンドでプレビューsceneと受領 `MiraifuFaceSeedWeb/Plugins/FaceSeed.jslib` の接続を出力する。受領UIの `miraifu:avatar-config/catalog/state` と、同一オリジン・対象windowを検証するHTMLブリッジを含める。Editor起動時の勝手なビルドを導入しない。
- [ ] `node scripts/import-avatar-v3.mjs --check`、Unityの `V3AssetTests`、`npm run build:avatar-v3` を実行する。6体、body/faceの対応、衣装参照、プレビュー起動が成功したら資産導入分をコミットする。

### Task 2: 本人別のV3外見・下書き・旧保存を分離する

**Files:** 新規 `shared/avatar-v3.ts`・`shared/avatar-v3-storage.ts`、受領shared定義。変更 `shared/demo-profile.ts`・`shared/account-avatar.ts`。試験 `tests/avatar-v3/storage.test.ts`・既存 `tests/account/avatar.test.ts`。

**Interfaces:** `V3Appearance = {version:3; assetProfile:'v3-unified-20261104'; choice:AvatarChoice}`。`isV3Appearance(value):value is V3Appearance`。`readV3Draft(storage, ownerId)`、`writeV3Draft(storage, ownerId, choice)`、`completeV3Avatar(storage, ownerId, nickname, appearance)`。既存 `AvatarAppearance` は旧型とV3型のunionにする。

- [ ] 旧v1/v2を保つ、全6体を認める、範囲外・未知モデルを拒否する、別人の下書きが読めない、閲覧で保存しない、保存失敗で旧完成値を保つ試験を先に書いて失敗を確認する。
- [ ] 受領マニフェストと `restoreAvatarChoice` を利用し、完成済みV3は非破壊で検証する。調整が必要な壊れた値を黙って完成値へ変換しない。
- [ ] 下書きキーを本人IDで分離する。完成値は既存の本人ID付き `DemoProfile` に保存し、旧完成値がある場合は退避が成功してから切り替える。プレビューACKや初期化では完成保存を呼ばない。
- [ ] Node単体試験と既存アカウント外見試験を実行し、成功後に保存境界をコミットする。

### Task 3: 現在のワールドへV3の本人描画を接続する

**Files:** 新規 `FutureCity/Runtime/Mirai/V3AvatarAdapter.cs`、変更 `ImportedAvatar.cs`・`PersonAnimator.cs`・`MiraiWorld.WebProfile.cs`・`WebProfileBridge.cs`・`FutureCity.asmdef`・プラグイン。試験 `FutureCity/Tests/Editor/V3WorldAvatarTests.cs`。

**Interfaces:** `V3AvatarAdapter.TryApply(WebChoice choice, out string error):bool`、`SetMotion(float speed, bool jumping):void`、`AppliedChoice`。新ブリッジenvelopeは旧 `appearance` と新 `appearanceV3` を排他的に受け付ける。適用ACKはモデルID・choice・body/faceメッシュを返す。

- [ ] 新旧データの分岐、全6体の適用、body/faceの一致、色、ルートの同一性、登録性別でV3を上書きしない試験を先に書いて失敗を確認する。
- [ ] `ImportedAvatar` の既存ルートを保つファサードからV3アダプターを呼ぶ。V3では旧Visualだけを無効化し、新しい描画子を作る。コライダーとRoot Motionを持ち込まない。旧形式へ戻る場合はV3描画だけを破棄して旧Visualを復元する。
- [ ] 既存の実移動速度・接地状態をアダプターへ渡し、V3のidle/walk/jumpを同期する。NPC経路と既存の着席を変えない。
- [ ] 未対応・不正な外見はエラーを返し、適用中の有効な見た目を維持する。認証用 `WebSessionBridge` を外見JSONで代用しない。
- [ ] Unityの `V3WorldAvatarTests` と既存InstalledAvatar/Wardrobe/Integrationの試験を実行し、現在のFutureCityをビルドしてコミットする。

### Task 4: `/avatar` をWebの6体選択・着せ替えへ差し替える

**Files:** 受領 `app/avatar/AvatarCustomizer.tsx`・`avatar.css`、変更 `app/avatar/page.tsx`、必要なメッセージブリッジ。試験 `tests/browser/avatar-v3-editor.spec.ts`。

**Interfaces:** `AvatarCustomizer({ownerId,nickname,assets})`。モデルは `getAllAvatarCandidates()` を使い、プロフィールの性別で絞らない。V3カタログ・適用stateはiframeのorigin/sourceと設定リビジョンを検証する。

- [ ] 未ログインの案内、プロフィール条件、全6体選択、初回／再読込で旧完成値を上書きしない、古いACKを無視する、失敗時は確定できない試験を先に書いて失敗を確認する。
- [ ] UI・画像を受領元から移植し、既存ロゴ・ログイン本人へ接続する。検証環境の男女fixtureリンク、別ワールドへのURL遷移、全利用者共通のlocalStorage保存を置き換える。
- [ ] 編集中はTask 2の本人別下書きへ保存する。確定時に現在のセッション本人を再確認し、同じ設定をプレビューが適用したACKを確認して完成保存する。保存失敗・本人変更・未対応選択では留まって入力を保持する。
- [ ] 確定後はクエリに衣装JSONを入れず `/plaza` へ進む。受領ビルドのカタログにない項目は有効な選択に見せない。
- [ ] エディターのブラウザ試験、型チェックを実行し、UI接続をコミットする。

### Task 5: ワールドとマイページへ同じ完成外見を引き継ぐ

**Files:** 変更 `UnityExperience.tsx`・`AccountAvatarPreview.tsx`・`shared/account-avatar.ts`、Unityの `Melife/index.html`、profile表示処理。試験 `tests/browser/avatar-v3-flow.spec.ts`・既存account外見試験。

**Interfaces:** 既存 `melife:initialize` のJS側でunionをTask 3の排他的envelopeへ変換する。`mode:'profile'` はV3描画をプレビューへ使うが保存・移動・本人プロフィール変更は行わない。

- [ ] editor→現在の街→マイページでモデルID・衣装・色が一致する、閲覧で保存しない、リロード／別人切り替えで混ざらない試験を先に書いて失敗を確認する。
- [ ] 新旧の完成外見を正しい経路へ渡し、profile専用カメラはV3も全身表示する。Reactの古いownerに対する初期化・応答を使わない。未保存・破損時は本人向け案内を出す。
- [ ] 旧NPC・街・ヘッダー・サーバーセッションを変更していないことを確認する。連携ブラウザ試験と関連account単体試験を実行し、引き継ぎをコミットする。

### Task 6: 実3D・認証・互換を統合確認して仕上げる

**Files:** `tests/avatar-v3/`・`tests/browser/avatar-v3-*.spec.ts`・Unity新旧試験、`package.json`、`unity/AVATARS.md`・`unity/README.md`・Web導線資料。

- [ ] 専用ローカルDBのテスト利用者で、全6体の選択、表情と衣装の適用、確定、現在の街への入場、マイページの同一外見を実WebGLで確認する。
- [ ] 実Unityでクリック、矢印キー／WASD、見回し、全画面、ログアウトと別利用者ログインを確認する。preview未完成・ストレージ不可・壊れた保存・旧保存・片方のビルド欠落も確認する。
- [ ] PC／小さい画面を撮影し、衣装を選んで確定するまで操作できることを目視確認する。
- [ ] `npm test`、V3関連単体試験、型・lint、関連Unity試験、preview/worldのWebGLビルド、Next.jsビルド、関連ブラウザ試験を実行する。失敗を残したまま完了扱いしない。
- [ ] 起動・再ビルド・本人別保存と旧互換の扱いを資料へ追記する。独立レビューの重要指摘を修正し、一時DB・キャッシュ副作用を片付ける。
- [ ] 既存環境の設定を保持して通常の開発サーバーを再開する。共有DBへのマイグレーション・mainへのマージ・プッシュはこの計画の実装完了に含めない。

## 計画の自己レビュー

設計の画面・保存・旧互換・Unity・依存資産・権限・試験をTasks 1〜6へ割り当てた。全6体、ログイン必須、旧データ非破壊、現在の街維持は全体制約で固定した。実装方式はこのチャット内で順に進める既存の方式を維持し、段階ごとの試験と最終の独立レビューを行う。
