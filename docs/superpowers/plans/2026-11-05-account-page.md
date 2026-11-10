# Account Page Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement task by task.

**Goal:** PCのマイページを左30％の3Dアバターと右70％のプロフィールに再構成する。
**Architecture:** サーバーで本人情報を取得し専用のAccountOverviewへ渡す。クライアントのAccountAvatarPreviewは本人に一致するブラウザ保存外見のみをUnityの読み取り専用profileモードへ送る。
**Tech Stack:** Existing Next.js 16.4.2, React, Unity 6000.4.2f1, Playwright.
**Spec:** docs/superpowers/specs/2026-11-05-account-page-design.md

## Global Constraints

- PCのみ、左30％・右70％。スマートフォン専用レイアウトは対象外。
- 本人の情報だけ表示。メール・得意なこと・目標はUnityへ渡さない。
- 外見の保存方式は変更しない。既存編集・ログアウト・ワールド導線を維持。
- 新しいモデル・依存ライブラリは追加しない。

## Review Focus

- 他人の保存外見・壊れた保存値を本人のアバターとして表示しない。
- Unityの読み込み失敗でもプロフィールは閲覧できる。
- 長いメール・分野・改行を含む自己情報でカラムを押し広げない。
- プレビューが保存済みの衣装を変更しない。
- 女性モデル未導入・外見未設定を空白画面にしない。

### Task 1: Read-only avatar preview

**Files:** shared/account-avatar.ts, app/components/AccountAvatarPreview.tsx, unity/FutureCity/Assets/FutureCity/Runtime/Mirai/{WebProfileBridge,MiraiWorld.WebProfile,MiraiWorld}.cs, Assets/WebGLTemplates/Melife/index.html
**Interfaces:** resolveAccountAvatar(storage, ownerId, nickname): DemoProfile | null. Unity mode='profile' consumes only display identity and appearance and does not publish changes.

- [x] Add tests for matching owner, foreign owner, missing/corrupt appearance, current nickname; run and observe failure.
- [x] Add Unity Decode profile test; run EditMode tests and observe failure.
- [x] Implement resolver, iframe bridge with origin/source checks, explicit missing/loading/error/retry states, and Unity read-only full-body preview.
- [x] Run resolver and Unity tests, rebuild WebGL, inspect real canvas with a saved customized appearance.

### Task 2: Desktop account overview

**Files:** app/account/page.tsx, app/account/account.css, app/components/AccountOverview.tsx, tests/browser/account-overview.spec.ts
**Interfaces:** AccountOverview consumes a safe projection of the current user's profile and an updated flag; composes AccountAvatarPreview.

- [x] Add desktop browser checks for 30:70 geometry, all profile information, long fields, notices, actions, missing avatar and preview failure; reproduce old layout failure.
- [x] Replace AuthShell with dedicated header, 30:70 grid, avatar card, readable profile cards, topic chips and account actions.
- [x] Run browser validation at 1440×1000 and 1280×800, inspect screenshots, run npm test, eslint and TypeScript.
- [x] Review complete diff against spec and record limitations. Leave changes reviewable in the user's current workspace; no automatic commit/merge.

## Execution notes

Use the existing workspace so the running site and installed Unity Library stay available. Keep the unrelated login configuration change intact. A local isolated authentication database is not configured; use a temporary development-only fixture for visual and bridge checks, remove it before delivery, and keep committed authenticated browser coverage requiring the existing explicit test DB.
