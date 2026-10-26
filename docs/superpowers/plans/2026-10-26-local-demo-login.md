# Local Demo Credential Login Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 既存フォームから登録済みテストアカウントでログインし、本人に対応する履歴とAI提案を利用する。

**Architecture:** サーバー専用資格情報JSONをscryptで照合する。ランダムなセッショントークンをHttpOnly Cookieで渡し、プロセス共有のメモリで検証する。既存の履歴Repositoryとローカル専用ガードを継続する。

**Tech Stack:** Next.js 14 / TypeScript / Node crypto・fs / node:test / Playwright。追加依存なし。

**Spec:** `docs/superpowers/specs/2026-10-26-local-demo-login-design.md`（承認済み）

## Global Constraints

- 登録済み固定3ユーザーのみ。新規登録・実会員認証・本番DB接続は対象外。
- 架空メールとテスト専用パスワードのみ。保存は個別saltを持つscryptハッシュ。APIにハッシュ・セッション一覧を返さない。
- ログイン後は `/ai-navi`。既存アバター・広場の体験機能は維持。
- ID指定ログインとユーザー選択ボタンを廃止する。通常dev・LAN・本番では無効。
- テストは固定seedと一時保存先のみ。実OpenAI通信なし。
- 現在のチェックアウトで実行する既存の作業環境方針を継続。開発サーバー停止後にビルド。

## Review Focus

- セッション固定・古いCookie：再ログインで古いトークンを失効し、ログアウト・期限切れ後の保存やAI利用を拒否（Task 1/2）。
- 開発ホットリロード：認証・AIの別モジュールでも同じセッション集合を参照（Task 2）。
- 資格情報の破損・過大入力：安全に失敗し、資格情報・実パスをレスポンスへ漏らさない（Task 1/2）。
- 別タブのログアウト中のAI応答：サーバー側でも完了時にセッションを再検証し、既存UIの古いGET破棄を維持（Task 2/3）。
- 旧体験データの再開リンク・ユーザー選択：新ログインを迂回する入口をログイン/AI画面に残さない（Task 3）。既存アバターの本番認可化までは含めない。

## Task 1: 資格情報とセッション

**Files:** Create `server/local-demo/credentials.ts`, `tests/local-demo/auth.test.ts`, `data/local-demo/credentials.json`, `tests/fixtures/local-demo/credentials.json`; Modify `server/local-demo/session.ts`。

**Interfaces:** `CredentialVerifier = (email:string,password:string)=>Promise<LocalUserId|null>`、`createCredentialVerifier(root:string):CredentialVerifier`。`LocalSessionStore` は `issue(userId,now):string`, `resolve(token,now):LocalUserId|null`, `revoke(token):void`。`getLocalSessionStore()` はプロセス共有singleton。`getSessionToken(request):string|null`、`sessionCookie(token:string|null,request):string`。Cookie名 `melife.local-demo.session.v2`、32乱数byteのhex、期限8時間、HttpOnly・SameSite=Lax・Path=/、HTTPSならSecure。テスト用にstoreを注入可能。

- [x] `auth.test.ts` に、正常・誤パスワード・未知メール・メール前後空白と小文字正規化・旧ID Cookie/重複Cookie拒否・失効/8時間期限・壊れたJSONをテストする。正常メール例 `sora@example.test`、同様に `aoi@example.test` / `haru@example.test`。テスト専用パスワード `MilifeDemo2026!`。
- [x] `node --import tsx --test tests/local-demo/auth.test.ts` で未実装の失敗を確認。
- [x] 上記契約を実装。資格情報は固定rootのcredentials.json（1MB上限、symlink拒否）から読み、schemaVersion1・既知id一意・メール一意・salt16byte・hash64byteを検証。scrypt N16384/r8/p1・timingSafeEqualを用い未知メールもダミーハッシュ照合。入力email最大254、password最大128。セッションは最大1000、有効期限掃除後に最古から削除。メモリ以外へ保存しない。
- [x] 同コマンドPASSと型チェック。Task 2まで旧選択API用関数は残し、このコミット単体の既存テストを維持。
- [x] 対象ファイルをコミット。

## Task 2: API認証の置換とAIセッション確認

**Files:** Modify `server/local-demo/handler.ts`, `server/local-demo/session.ts`, `server/local-demo/http.ts`, `server/ai-navi/demo-handler.ts`, `app/api/local-demo/route.ts`, `tests/local-demo/handler.test.ts`, `tests/local-demo/ai-context.test.ts`, `tests/ai-navi/demo-handler.test.ts`。

**Interfaces:** Task 1を利用。`createLocalDemoHandler` optionsに `verify?:CredentialVerifier`, `sessions?:LocalSessionStore` を追加、既定は本番と同じ固定rootからのverifierとsingleton。`createDemoHandler` に `sessions?:LocalSessionStore`。POST login契約は厳密に `{action:'login',email:string,password:string}`。GET・record・logoutのstate契約は既存通り。既定資格情報rootは `MILIFE_LOCAL_DEMO_DIR ?? process.cwd()/data/local-demo`。

- [x] 正常ログイン200/Cookie、誤パスワードと未知メールは同じ401、旧 `{userId}` ログイン400、偽造/失効Cookieのrecord・AI401、別ユーザー非混在、ログアウト失効、連続失敗429をテスト。失敗はプロセス共有・全体で10分20回まで、成功ログインは失敗カウントをリセットしない。時間窓後に回復。ホットリロード相当の異なるhandlerインスタンス間でセッションを共有するテストも追加。
- [x] 対象テストを実行し旧契約での失敗を確認。
- [x] 全APIを検証済みsession経由へ変更。成功時に旧tokenをrevoke、stateの読込みが成功してから新tokenを発行する。logoutでrevokeしCookie削除。入力・Host/Originガードを維持し、入力値をログしない。資格情報の破損はサニタイズした500。
- [x] AI生成の前後でtokenのsession有効性を照合し、処理中ログアウト/期限切れなら401で結果を破棄。旧ユーザーID Cookie関数を削除。テストは新session発行ヘルパーへ更新。
- [x] `npm run test:local-demo` と `npm run test:ai-navi`、型チェックPASS。対象ファイルをコミット。

## Task 3: 既存フォーム接続・画面・開発資料

**Files:** Modify `app/components/LoginForm.tsx`, `app/components/auth.css`, `app/ai-navi/LocalDemoWorkspace.tsx`, `shared/local-demo-client.ts`, `tests/browser/local-demo-fixture.ts`, `tests/browser/local-demo.spec.ts`, `tests/browser/ai-navi.spec.ts`, `tests/browser/auth.spec.ts`, `docs/ai-navi-development.md`, `data/local-demo/README.md`, `docs/requirements.md`; Delete `app/components/LocalDemoLogin.tsx`。

**Interfaces:** `changeLocalState` の既存JSON送信を使い `{action:'login',email,password}` をPOST。成功時router.push('/ai-navi')。AI画面は切替selectを廃止し、未ログインは `/login` へのリンクを表示。テスト `selectUser` は固定ID→テストメールへ変換し、実際にメール/パスワードAPIで認証する。

- [x] ブラウザで既存フォームの誤入力→エラー→正常ログイン→AI履歴表示→再読込→ログアウト→再ログインをテスト。既存セッション切替・遅延GET・古いタブの履歴保存テストを、別ログイン/別タブ操作へ置換。テストが試験保存先を照合する仕組みは維持。
- [x] 修正前に新フォームテストの失敗を確認。
- [x] フォームをAPIへ接続し送信中の二重操作を抑止、パスワードをlocalStorage等へ保存せず送信後にクリア。未接続表示・選択ボタン・旧ブラウザデータの認証迂回的な再開リンクを削除。通常起動は開発認証が使えない旨を表示。AI画面はsession失効後にログイン案内を示し、旧応答の操作世代・revisionガードは維持。
- [x] 資料にテスト専用資格情報、保存ファイル、セッション再起動失効、localhost起動・Git共有・本番認証対象外を追記。デスクトップ/スマホでフォーム確認。
- [x] `npm test`, `npx tsc --noEmit`, 対象PlaywrightテストPASS。サーバー停止後 `npm run build`。共有JSON差分なしを確認し通常の専用開発サーバーへ戻す。
- [x] 対象ファイルをコミットし、全体レビューと重要指摘の修正を行う。自動pushしない。

Self-review: 承認済み設計の範囲・インターフェース・Review Focusと各テストの対応を確認済み。
