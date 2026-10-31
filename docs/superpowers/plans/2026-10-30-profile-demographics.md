# Profile demographics Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 必須性別・18歳以上の年齢入力・年齢バッジ・男女別モデル接続を実装する。
**Architecture:** 既存UserProfile、入力検証、Unityセッションを拡張。年齢の公開はバッジに限定する。
**Tech Stack:** Next.js 16 / React / Prisma / Unity C#
**Spec:** docs/superpowers/specs/2026-10-30-profile-demographics-design.md

## Global Constraints
18歳以上。男性／女性は必須。バッジは18–29/30–49/50–59/60以上。着ぐるみ共通。

## Review Focus
既存NULLプロフィールの編集読み込み、年齢境界、不正性別、年齢漏洩、女性素材不足。

### Task 1: 保存・検証
- [x] 年齢境界・性別必須の失敗テストを追加し実行。
- [x] shared/profile-demographics.ts、shared/account-profile.ts、signup検証、profile-store、Prisma列と移行を更新。
- [x] npm run test:account と型検査を実行。

### Task 2: UI・ワールド
- [x] 登録・編集に必須性別と年齢を追加。旧NULLは読み込めるが保存不可にする。
- [x] マイページ・Unityプロフィールをバッジ表示にする。セッションは性別とバッジのみ追加。
- [x] 男女モデル接続と不足表示を追加、共通着ぐるみを維持。

### Task 3: 要件・検証
- [x] 要件定義の旧実年齢表示を承認済み変更で置換し、未成年登録案を対象外にする。
- [x] npm test、npx tsc --noEmit、変更ファイルのlintを実行。
- [x] Unityビルド／テストの可否と素材不足を記録。

## 実行記録
ユーザーは仕様とコード編集を承認済み。同じ作業ディレクトリで既存変更を維持して実装する。自動コミット・別worktree作成は行わない。

### 検証結果
- `npm test`: 115件成功（51+32+23+9）。
- Unity EditMode: 32件成功。WebGL再ビルド成功。
- `playwright.demographics.config.ts`: Chrome1件成功（APIをモックし、必須入力・年齢制限・バッジ・PATCH送信・失敗時の入力保持を確認）。
- TypeScript: 既存の`.next`内の`routes.d 2.ts`／`routes.d 3.ts`によるLayoutProps重複を除外した一時設定で成功。通常設定の型検査はこの既存重複で失敗。
- 独立レビュー: 直接Unity URLからの必須入力回避を指摘、セッションAPIとテンプレートの編集画面誘導を追加し再レビュー通過。
- DBテストは`DATABASE_TEST_URL`未設定で未実行。接続先DBの移行状況照会は`_prisma_migrations`の権限不足で失敗、DB移行は未適用。
- 女性用モデル素材がないため、女性通常アバターの実表示は未完了。女性モデル接続と不足案内・共通着ぐるみは実装済み。
