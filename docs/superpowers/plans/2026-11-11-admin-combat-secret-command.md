# 管理者用隠し戦闘コマンド 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** サーバーが許可したメール検証済み管理者だけが、コナミコマンドでローカル戦闘モードを切り替えられるようにする。

**Architecture:** `/api/unity/session` は認証済みアカウントの検証済みメールとサーバー環境変数を照合し、メールを含めず`profile.canUseCombat`だけを返す。Unityのセッションブリッジはこの権限を保持し、権限がなくなると戦闘を停止する。戦闘入力はFキーから`↑ ↑ ↓ ↓ ← → ← → B A`へ変更する。

**Tech Stack:** Next.js 16 Route Handler、BetterAuth、Prisma、Unity 6 WebGL、Unity Test Framework、Node.js `node:test`、Playwright。

**Spec:** `docs/admin-combat-secret-command-design.md`

## Global Constraints

- `UNITY_COMBAT_ADMIN_EMAILS`はカンマ区切りとし、空なら許可しない。
- メール照合前に前後空白を除去し、小文字化する。メールが未検証なら許可しない。
- クライアントから送られるメールやユーザーIDを権限根拠にしない。
- 一般向けUIやヘルプに隠しコマンドを表示しない。
- 戦闘はローカル演出だけで、サーバー状態、報酬、ユーザーデータを変更しない。
- DBスキーマとマイグレーションを追加しない。
- Next.js変更前に`node_modules/next/dist/docs/`の関連Route Handlerガイドを確認する（確認済み）。

## Review Focus

- 未認証ユーザーは許可されない — Task 1 API/helper testとTask 3 guest browser test。
- 未検証または非許可メールは許可されない — Task 1 unit test。
- 空・空白・大小文字が異なる許可リスト入力を安全に扱う — Task 1 unit test。
- 管理者権限が同一ユーザー上で失効したら戦闘状態も停止する — Task 2 PlayMode test。
- ゲストや非管理者がFまたは正しいキー列を押しても戦闘にならない — Task 2 EditMode testとTask 3 browser test。

---

### Task 1: サーバー側の管理者許可判定

**Files:**
- Create: `server/auth/unity-combat-access.ts`
- Modify: `app/api/unity/session/route.ts`, `server/auth/server.ts`
- Test: `tests/account/unity-combat-access.test.ts`
- Modify: `.env.example`

**Interfaces:**
- Produces: `canUseUnityCombat(email: string | null, emailVerified: boolean, allowlist?: string): boolean`
- API response: authenticated profileに`canUseCombat: boolean`を含め、未認証responseの形は現状どおりに保つ。

- [ ] **Step 1: RED — helperのテストを先に書く。**
  検証済みメールの一致、前後空白と大文字小文字の正規化、未検証メール、非許可メール、nullメール、空または空白だけの環境値をテストする。
- [ ] **Step 2: REDを確認する。** `node --import tsx --test tests/account/unity-combat-access.test.ts`を実行し、関数未実装で失敗することを確認する。
- [ ] **Step 3: GREEN — `canUseUnityCombat`を実装する。** 引数が省略された場合のみ`process.env.UNITY_COMBAT_ADMIN_EMAILS`を使う。メールはクライアントへ返さない。
- [ ] **Step 4: API統合する。** `/api/unity/session`が`getCurrentUser`で検証済みのDBユーザーを使い、許可フラグだけを返すことを確認する。route handlerは既存のNode testから分離しにくいため、権限規則は純関数テスト、配信データはブラウザーテストで検証する。
- [ ] **Step 5: APIと運用値を実装する。** `getCurrentUser`のDB選択に`emailVerified`を加え、routeでhelperを呼ぶ。`.env.example`に空の`UNITY_COMBAT_ADMIN_EMAILS`と用途コメントを追加する。
- [ ] **Step 6: GREENを確認する。** `node --import tsx --test tests/account/unity-combat-access.test.ts`と該当するaccountテスト群を実行する。
- [ ] **Step 7: Commit** — helper、route、テスト、環境変数例を一緒にコミットする。

### Task 2: Unityのセッション権限と隠し入力

**Files:**
- Create: `unity/FutureCity/Assets/FutureCity/Runtime/Core/SecretCombatCommand.cs`と`.meta`
- Modify: `unity/FutureCity/Assets/FutureCity/Runtime/Core/InputBridge.cs`
- Modify: `unity/FutureCity/Assets/FutureCity/Runtime/Mirai/WebSessionBridge.cs`, `unity/FutureCity/Assets/FutureCity/Runtime/Mirai/MiraiWorld.cs`
- Modify: `unity/FutureCity/Assets/FutureCity/Runtime/Systems/CombatSystem.cs`, `unity/FutureCity/Assets/FutureCity/Runtime/Systems/PlayerWalker.cs`
- Test: `unity/FutureCity/Assets/FutureCity/Tests/Editor/CombatSecretCommandTests.cs`と`.meta`
- Test: `unity/FutureCity/Assets/FutureCity/Tests/PlayMode/CombatSecretCommandPlayModeTests.cs`と`.meta`
- Modify: `unity/FutureCity/Assets/FutureCity/Tests/Editor/FutureCity.AvatarTests.asmdef`、PlayMode asmdef

**Interfaces:**
- Consumes: Task 1のAPI `profile.canUseCombat`。
- Produces: `SecretCombatCommand.Feed(FKey key)`, `WebPlayerProfile.canUseCombat`, `CombatSystem.SetCommandAccess(bool allowed)`。

- [ ] **Step 1: RED — コマンド認識器のテストを書く。** 正確な10キー列だけが一度完了し、途中の誤入力後に再スタートできること、Fが列に含まれないことをテストする。
- [ ] **Step 2: REDを確認する。** Unity Editor testを実行し、コマンド認識器が未実装で失敗することを確認する。
- [ ] **Step 3: GREEN — `SecretCombatCommand`を実装する。** 入力列は`Up, Up, Down, Down, Left, Right, Left, Right, B, A`。`InputBridge`にBを追加し、新旧Input Systemの両方に割り当てる。
- [ ] **Step 4: GREENを確認する。** コマンド認識Editor testを実行する。
- [ ] **Step 5: RED — `CombatSystem`認可テストを書く。** 権限falseなら完全な列でもActiveにならず、trueなら列完了で切り替わり、falseへ戻すとActiveが停止するPlayMode testを追加する。
- [ ] **Step 6: REDを確認する。** PlayMode testを実行し、認可入力が未実装で失敗することを確認する。
- [ ] **Step 7: GREEN — セッション権限をUnityへ接続する。** `WebPlayerProfile`から`MiraiWorld`へbooleanを渡す。未認証、別ユーザー、権限失効時にCombatSystemを停止する。
- [ ] **Step 8: GREEN — 戦闘入力を置き換える。** `CombatSystem.Update`は認可済みの時だけコマンド認識器を進め、完了時にToggleする。Fキー経由の切り替えを削除する。戦闘中のHUDからFキー表記を削除する。
- [ ] **Step 9: GREENを確認する。** EditorとPlayModeの該当テスト、既存戦闘・歩行テストを実行する。
- [ ] **Step 10: Commit** — Unityのブリッジ、入力、認可、テストを一緒にコミットする。

### Task 3: ブラウザー検証、運用説明、WebGL配信

**Files:**
- Modify: `tests/browser/world-combat.spec.ts`, `playwright.combat.config.ts`, `package.json`
- Modify: `docs/admin-combat-secret-command-design.md`, `docs/world-combat.md`
- Output: `.gitignore`対象の`public/unity/FutureCity/Build/`

**Interfaces:**
- Consumes: Task 1の`profile.canUseCombat`とTask 2の隠し入力。
- Produces: browser tests for guest, authenticated non-admin, authorized admin, F rejection, and revocation/reset.

- [ ] **Step 1: RED — ブラウザーケースを追加する。** ゲストと非管理者ではFとコナミコマンドが無効、許可管理者はコナミコマンドでON/OFFしFでは切り替わらない、認証プロフィール変更で停止するケースを書く。
- [ ] **Step 2: REDを確認する。** `npm run test:combat`で新しい期待値が現行実装に対して失敗することを確認する。
- [ ] **Step 3: GREEN — 必要なテスト補助だけ追加する。** APIセッションをブラウザー内で模擬する。実DBには書き込まない。
- [ ] **Step 4: GREENを確認する。** `npm run test:combat`と`npm run test:room-topics`を実行する。
- [ ] **Step 5: 運用説明を更新する。** Fキー説明を削除し、管理者のメール許可リスト設定と「WebGL内の表示制限でありサーバー操作の権限境界ではない」ことを記す。一般UIやHelpにはコードを掲載しない。
- [ ] **Step 6: Unity WebGLをクリーンビルドする。** `MELIFE_UNITY_CLEAN_BUILD=1 npm run build:unity`を実行し、bundle4ファイルをローカル配信先に反映する。
- [ ] **Step 7: 最終検証** — `npm test`、Unity Editor/PlayMode tests、`npm run test:combat`、`npm run test:room-topics`、TypeScript、ESLintを実行し、結果を記録する。
- [ ] **Step 8: Commit** — ブラウザーテスト、ドキュメントとコード変更をコミットし、作業ブランチをレビューする。

---

## Execution Ledger

- Pre-flight: Task 1 produces `profile.canUseCombat`; Task 2 consumes it in the Unity session bridge; Task 3 consumes the API field and hidden input. These interfaces are consistent.
