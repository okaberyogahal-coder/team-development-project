# Avatar Brand Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** ログイン後のアバター設定画面を、Unity内部までTOPのロゴ・配色・カード表現と統一する。

**Architecture:** `/avatar` 専用Web CSSと既存Logoで外枠を統一。Unityは作成モード専用のGUIスタイルと背景を用い、既存の衣装データ・保存・ワールド移動の経路を保つ。WebGL成果物を実際に再生成して操作確認する。

**Tech Stack:** Next.js / React / CSS、Unity 6000.4.2f1 / C# IMGUI、Unity Editor Tests、Playwright。追加依存なし。

**Spec:** `docs/superpowers/specs/2026-10-27-avatar-brand-design.md`（2026-10-27承認済み）

## Global Constraints

- TOPのロゴ画像・濃い青 `#153b78`・青 `#0755cf`・黄色 `#ffe04b`・水色ドット背景・ピンクアクセントを使う。
- 作成モードだけの変更。ワールドの通常UI、衣装色スウォッチの意味、ログイン/AIの仕様は変更しない。
- カテゴリ・服装・顔/髪・小物・コスチューム・色・回転・おすすめ・保存・入場を維持する。
- 追加画像生成・ライブラリ不要。衣装のDB保存は対象外。
- 既存作業環境方針に従い現在のチェックアウトで実装。Webビルド時は開発サーバー停止。
- 共有履歴を試験で変更しない。Unity配信物はgitignore対象なのでソースをコミットし、ローカル成果物を再生成する。

## Review Focus

- Unityの起動後に作成モードが指定されても新テーマが有効になること。スタイル初期化の時期に依存しない（Task 2）。
- 作成からワールドへ進むと通常GUIへ戻ること。共通GUIスタイルやキャッシュを永続変更しない（Task 2/3）。
- 390px幅と低いPC画面でもタブ・色・入場ボタンに到達できること（Task 2/3）。
- 明るい黄色・白のスウォッチと無効項目の判別を保ち、選択状態を色だけに依存しない（Task 2/3）。
- WebGL配信キャッシュに旧ビルドが残らず、新テーマの実行結果を確認できること（Task 3）。

## Task 1: TOPに合わせたWeb外枠

**Files:** Modify `app/avatar/page.tsx`, `app/components/UnityExperience.tsx`; Create `app/avatar/avatar.css`; Test `tests/browser/avatar-brand.spec.ts`。

**Interfaces:** `/avatar` のrootに `avatar-setup-page`。既存 `Logo` を再利用。`UnityExperience` の `mode==='avatar'` 案内にのみ専用クラスを付ける。iframe titleとメッセージ契約は維持。

- [x] ブラウザテストを先に作成。ブラウザ用架空プロフィールをseedし `/avatar` を開く。TOPと同じロゴURL、青い見出し、ワールド入場前の案内、iframeの存在、横幅390/1440で横はみ出しなしを確認。`/plaza` は専用クラスを持たないことも確認。
- [x] `npx playwright test tests/browser/avatar-brand.spec.ts --retries=0` で旧画面の失敗を確認。
- [x] ロゴと「TOPへ戻る」、黄色ラベル、見出し「あなたらしい姿で、はじめよう。」、水色ドット背景、ピンクアクセント、角丸のUnity表示領域を実装。説明文は短くし、衣装操作の表示高さを優先。既存のブラウザ保存に関する説明を正しく維持。
- [x] 同ブラウザテストと `npx tsc --noEmit` PASS。対象ファイルをコミット。

## Task 2: Unity作成モードのテーマと表示領域

**Files:** Create `unity/FutureCity/Assets/FutureCity/Runtime/Mirai/MiraiWorld.AvatarTheme.cs` と対応meta; Modify `MiraiWorld.WebProfile.cs`, `MiraiWorld.Wardrobe.cs`, `MiraiWorld.cs`（同Runtime/Mirai配下）; Create `unity/FutureCity/Assets/FutureCity/Tests/Editor/AvatarThemeTests.cs` と対応meta。

**Interfaces:** 作成用テーマの生成/背景描画を新partialファイルへ集約する。`DrawWebAvatarCreation()` が作成用のFrameを使う。`Btn` と衣装用テキスト/プレビュー部品は作成中だけ専用スタイルを選択し、それ以外は既存スタイルを使う。`AvatarWardrobeLayout.Calculate(float width,float height)` は表示領域からpanel/preview/tabs/choicesの矩形・縦スクロール範囲を返す純粋ヘルパーとして定義する（同ファイルまたは小さな専用ファイル）。

- [x] Editorテストを先に作成。幅/高さ1440×700・1024×420・390×590相当で生成する表示矩形が負サイズにならず領域内に収まり、プレビュー・タブ・選択領域が重ならないことを確認。モード切替後も通常UIのスタイル参照を維持する境界をテストする。
- [x] Unity `-batchmode -nographics -projectPath unity/FutureCity -runTests -testPlatform EditMode -testFilter FutureCity.Tests.AvatarThemeTests -testResults <tmp>/avatar-theme-red.xml -logFile <tmp>/avatar-theme-red.log` で未実装の失敗を確認（runTestsにはquitを付けない）。
- [x] 作成専用の青い文字・輪郭、白/クリーム色の角丸カード、青い選択/入場ボタン、水色の背景とドット、黄色/ピンク装飾を実装。既存Rounded/Solid関数を利用し、テクスチャ/GUIStyleはキャッシュする。毎フレームの新規テクスチャ生成や共通styleの書換えを避ける。
- [x] 実色スウォッチと✓印を維持。小さい表示ではプレビューと設定を縦に並べ、タブを折り返すなどして操作可能にする。低い画面は設定領域をスクロール可能にし、入場ボタンをスクロール外の到達可能な場所に置く。通常モードのUiScaleとワールドGUIには影響させない。
- [x] `FutureCity.Tests.AvatarThemeTests` と既存 `FutureCity.Tests.WardrobeTests` / `AvatarIntegrationTests` / `InstalledAvatarTests` をEditorで実行しXMLの失敗0を確認。必要なUnitymetaを含め対象ソースをコミット。

## Task 3: WebGL再ビルドと実操作確認

**Files:** Modify `tests/browser/avatar-brand.spec.ts`, `docs/demo-onboarding.md`; 生成物 `public/unity/FutureCity/` は既存gitignoreに従いコミットしない。

**Interfaces:** 既存 `npm run build:unity` は `FutureCityWebBuild.Build` を実行して配信ディレクトリへ出力する。既存の `melife:appearance` / `melife:complete` メッセージと `/plaza` 遷移を維持。

- [x] `npm run build:unity` を実行しログの succeeded と生成ファイルの更新を確認。失敗時は原因を特定して修正し、旧生成物を新成果と扱わない。
- [x] 実WebGLを読み込むPlaywrightテストを作成。新背景と青い作成UIが見えるスクリーンショットを保存し、カテゴリ切替・衣装/色選択・回転・おすすめ・入場を実操作する。localStorageに保存されたappearanceが変わり、完了で `/plaza` へ進むことを確認。任意の待ち秒数で完了とせず、初期化状態・画面/保存結果を待つ。
- [x] PC1440px・低いPC1024×600・390px幅を実表示で確認。TOPとのロゴ・配色の整合、文字と選択状態、スクロール・入場ボタンの到達性を画像で確認。必要なら調整して該当テストを再実行。
- [x] `npm test`、`npx tsc --noEmit`、関連ブラウザテストを実行。開発サーバー停止後 `npm run build`。共有JSONに差分がないことを確認し、通常の専用起動へ戻す。
- [x] 実装範囲・Unity再ビルド・確認手順を資料へ追記しコミット。最終全体レビューで重要指摘を修正する。自動pushしない。

Self-review: 承認済み設計の全項目と各タスクの検証・インターフェースの対応を確認済み。
