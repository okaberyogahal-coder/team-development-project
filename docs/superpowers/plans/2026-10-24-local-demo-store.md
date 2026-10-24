# 共有JSONによる開発用ログイン・履歴保存 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gitで共有できる架空ユーザーと学習履歴をJSONへ保存し、開発用ログインからAI未来ナビまでつなぐ。
**Architecture:** 保存先をLocalDemoRepositoryに隔離する。Cookieは架空ユーザーの選択状態だけを保持し、APIはCookieのユーザーを対象に履歴を更新する。AI入力と人気集計は同じ保存済み履歴から構成する。
**Tech Stack:** 既存Next.js 14、TypeScript、Node.js fs/promises、node:test、Playwright。追加依存なし。
**Spec:** `docs/superpowers/specs/2026-10-24-local-demo-store-design.md`

## Global Constraints

- 固定の架空3ユーザー・サンプル教材だけ。本物のメール・パスワードは保存しない。本人認証ではなく開発用ユーザー切替。
- JSONのみGit共有。Cookie・ログイン状態・実個人情報は共有ファイルに保存しない。
- 専用dev:ai-naviのループバック起動のみ有効。通常dev・LAN・本番は新規APIも404。
- 閲覧だけで完了にしない。保存の失敗・破損を空履歴と偽らない。
- 単一プロセス内で直列化し、同一ディレクトリ一時ファイル→renameで置換。手編集・Git操作の前にサーバー停止。
- DB差し替え用のRepository境界。DB側のスキーマや本番認証との互換を保証しない。
- ブラウザテストはデータの一時コピーを使い、共有JSONに書き込まない。

## Review Focus

- 同時保存・同一要求の再送でも履歴を消失/重複させない（Task1）。
- 壊れた・欠落したJSONを初期化で上書きしない（Task1）。
- ログアウト後・未知Cookie・本文の他ユーザーIDで書込/AI要求をしない（Task2）。
- 受講記録変更中の旧AI応答を採用しない（Task2・3）。
- テストやGit操作が共有サンプルを破損・黙示リセットしない（Task1・3）。

## 共通型とファイル

`shared/local-demo.ts`：固定user IDs（photography / food-photo / newcomer）、`LocalEvent {id:string; materialId:string; kind:'view'|'complete'; at:string}`、`LocalUser {schemaVersion:1; id:LocalUserId; nickname:string; description:string; events:LocalEvent[]}`、`LocalDemoState {users:{id,nickname,description}[]; currentUser:LocalUser|null; materials:Material[]; popularity:PopularitySummary; revision:string}`。

`data/local-demo/materials.json` は `{schemaVersion:1,materials:Material[]}`。3ユーザーはそれぞれ `users/<ID>.json`。初期日時は2026-10-24T00:00:00.000Zとし、実行時に更新しない。初期は写真2人・花1人・料理1人、履歴なし1人。人気は実ファイル内の記録から計算し、固定の5人集計は本経路で使わない。

### Task 1: JSONとRepository

**Files:** Create `shared/local-demo.ts`, `server/local-demo/repository.ts`, `server/local-demo/state.ts`, `data/local-demo/materials.json`, `data/local-demo/users/{photography,food-photo,newcomer}.json`, `tests/local-demo/repository.test.ts`。Modify `package.json`, `.gitignore`。
**Interfaces:** `LocalDemoRepository {listUsers():Promise<LocalUser[]>; listMaterials():Promise<Material[]>; getUser(id:LocalUserId):Promise<LocalUser>; recordEvent(id:LocalUserId,materialId:string,kind:LocalEvent['kind'],now:Date):Promise<LocalUser>}`。`FileLocalDemoRepository(root:string)` 実装。`loadLocalDemoState(repo, userId:LocalUserId|null, now:number):Promise<LocalDemoState>`、`buildStoredContext(state:LocalDemoState):DemoContext`。

- [ ] 一時ディレクトリへコピーした実JSONでテストを書く。記録して別インスタンスで読んでも復元、閲覧/完了の区別、同一kind+教材は1件へ更新、異なる教材の同時更新を保持、未知ID・教材・破損・欠落の拒否、破損時元ファイル不変を確認。初期人気はnatural-lightが100%、各花/料理は50%。30日後は古い初期履歴を勝手に再日付せず集計対象外とする。
- [ ] `node --import tsx --test tests/local-demo/repository.test.ts` で未実装の失敗を確認。
- [ ] 型・JSON・Repositoryと集計/AI入力変換を実装。固定ID許可リストでパスを構築し、JSON型・重複ID・教材参照・ISO日時・上限（1MB、最大1000events）を検証する。全イベントを読み直した最新データから更新し、プロセス共有の保存キューで直列化する。tmpファイルは成功/失敗時に整理しGit対象外にする。revisionは選択ユーザーと教材・全履歴を正規JSON化したSHA-256とする。
- [ ] 同テストPASS。`test:local-demo` を追加し既存 `npm test` に接続。
- [ ] 対象ファイルをコミット。

### Task 2: 選択Cookie・履歴API・保存済み履歴によるAI

**Files:** Create `server/local-demo/http.ts`, `server/local-demo/session.ts`, `server/local-demo/handler.ts`, `app/api/local-demo/route.ts`, `tests/local-demo/handler.test.ts`, `tests/local-demo/ai-context.test.ts`。Modify `server/ai-navi/demo-handler.ts`, `app/api/ai-navi/demo/route.ts`, `tests/ai-navi/demo-handler.test.ts`。
**Interfaces:** `createLocalDemoHandler({repo,environment,enabled,now?}):(Request)=>Promise<Response>` をGET/POSTで使用。Cookie名`melife.local-demo.user.v1`、HttpOnly、SameSite=Lax、Path=/、7日、HTTPS時のみSecure。`getSelectedLocalUser(request):LocalUserId|null` はCookieを許可リストと照合。`getLocalDemoRepository()` はルート固定の共有インスタンス。

- [ ] テストを先に書く。GETはユーザー一覧と未選択state、POST `{action:'login',userId}` はCookie、`{action:'logout'}` はCookie削除、`{action:'record',materialId,kind}` は選択ユーザーだけに保存する。未選択401、壊れた選択・未知ID拒否、他ユーザーIDや任意日時の余分な入力400、Origin/Host違反403、開発以外404、1KB超413を確認。ログアウトしてもJSONは不変。
- [ ] `node --import tsx --test tests/local-demo/*.test.ts` の新規テスト失敗を確認。
- [ ] ハンドラー実装。GETもHostを検証し、POSTはOrigin完全一致。本文はストリーム最大1024バイト、JSON以外415、no-store。保存失敗は安全なコード/説明で500を返し実パスを返さない。選択状態の変更以外はJSONへの書込み口をrecordに限定する。
- [ ] AIハンドラーはrepoを受け取り、本文は空オブジェクトのみとする。Cookieから現在ユーザーを取得し、保存済みstateでAI入力と人気を生成する。未選択401、取得失敗500。要求時の選択ユーザーで計算した実行前revisionと、生成後に同じユーザーで再取得した最新revisionを照合し、データ変更済みなら409 STALEを返す。別リクエストで変わったブラウザCookieはこの要求から読めないため、選択変更はTask3のUIで最新stateと照合する。既存の設定不足・回数制限・同時実行・上流エラーを維持。API返却には実行時revisionを含める。
- [ ] AI境界のテストでクライアント指定case IDを受け付けないこと、選択ユーザーの履歴が生成に使われること、保存変更時409、個人/人気の混同なしを確認する。旧固定サンプル生成は純粋単体テスト用に残してよいが、実API経路からのフォールバックは禁止。
- [ ] テストPASS、型チェック。対象ファイルをコミット。

### Task 3: ログイン画面と教材操作、実履歴表示

**Files:** Create `app/components/LocalDemoLogin.tsx`, `app/ai-navi/LocalDemoWorkspace.tsx`, `tests/browser/local-demo.spec.ts`。Modify `app/components/LoginForm.tsx`, `app/ai-navi/page.tsx`, `app/ai-navi/DemoClient.tsx`, `app/ai-navi/demo.css`, `tests/browser/ai-navi.spec.ts`, `docs/ai-navi-development.md`, `docs/demo-onboarding.md`, `docs/requirements.md`。必要な表示共有型は`shared/local-demo.ts`。
**Interfaces:** `/login`の開発用セクションがGETで一覧を取得、POST login→`/ai-navi`。非専用起動の404はセクションを表示しない。`LocalDemoWorkspace` がGET stateと選択・ログアウト・教材操作を担当し、現在userのhistoryを`DemoClient`へ渡す。AI POST `{}`。既存の未来ナビのテーマ/理由/根拠/任意教材の表示は維持する。

- [ ] ブラウザテストを先に更新。未選択→開発用ユーザー選択→ログイン→未来ナビ、再読込復元、教材詳細を開いて閲覧だけ記録、完了は明示操作、別ユーザーで履歴非混在、ログアウト後更新不可、再ログインで保持を確認。AI成功表示は通信境界のみ差し替え、外部送信しない。切替・履歴変更・ログアウト中の旧AI応答を破棄するテストを維持する。
- [ ] 旧画面で該当テストの失敗を確認。
- [ ] 画面実装。GET/POST結果が成功してから状態を更新し、失敗を空履歴にしない。選択CookieはUIへ直接読み書きせずAPIを使う。state/revision更新でAI要求をAbortし、戻ったrevisionとも照合する。別タブの選択変更も、生成完了前のstate再取得で確認し古い結果を採用しない。教材クリックでview保存、詳細内のボタンでcomplete保存。同一人の再閲覧は日時更新と表示する。
- [ ] ブラウザテストは一時コピーを `MILIFE_LOCAL_DEMO_DIR` に指定して専用サーバーを起動する。固定rootの差し替えはサーバー環境変数だけ許可し、ブラウザからパスを受け取らない。テスト後のGit差分で本物の共有JSONが変わっていないことを確認。
- [ ] `npm test`、`npx tsc --noEmit`、ブラウザテストとデスクトップ/スマホ表示を確認。開発サーバー停止後 `npm run build`。検証用サーバー停止後、通常の共有JSONで専用起動し直す。
- [ ] 資料にJSON編集・共有・競合解決前の停止・起動コマンド・実認証でないこと・DB差し替え境界・実API品質は未検証を記載。承認済み設計と計画・要件をコミットする。

## Self-review

データ保存/復元はTask1、Cookieとアクセス・競合応答はTask2、ユーザー体験とGit非汚染はTask3で検証する。JSONの永続履歴とブラウザ選択状態を混同しない。今後DBを接続する際は会員ID・本番認証・履歴移行の合意が必要。メール/パスワード・Unity外見のファイル保存は今回追加しない。
