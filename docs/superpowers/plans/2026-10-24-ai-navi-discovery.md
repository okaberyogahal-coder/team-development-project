# 履歴から趣味・テーマを発見する Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 希望の申告なしで履歴の関連性から趣味・テーマを提案する体験版に変更する。
**Architecture:** 既存のOpenAI Responses API・開発専用APIを維持し、履歴入力と提案出力を差し替える。画面ではテーマ・理由・最初の一歩を主表示し、関連教材を任意の補助案内にする。
**Tech Stack:** Next.js 14 / React / TypeScript / node:test / Playwright。追加依存なし。
**Spec:** `docs/superpowers/specs/2026-10-24-ai-navi-discovery-design.md`

## Global Constraints

- 本人が希望を説明することを前提にしない。履歴の捏造と閲覧→完了の昇格を拒否する。
- 最大3テーマ。教材なしでも提案可能。関連教材IDは公開カタログと照合。
- 履歴なしは直近30日・利用率80%以上の人気を入口とする。個人履歴と人気を区別する。
- 専用ローカル起動、APIキー管理、20秒打切り、60秒3回、切替時の古い結果破棄を維持する。
- ビルド前に開発サーバーを停止し、確認後に専用コマンドで再起動する。

## Review Focus

- 閲覧を受講完了とする根拠の捏造を拒否（Task 1）。
- 履歴なしで個人の関心を発見したという根拠を拒否（Task 1）。
- 組合せを変えても同じ固定データをAIへ送らない（Task 2）。
- 教材なしテーマと不正教材参照を区別（Task 1・3）。
- 切替後の古い応答とHTML文字列を実表示に持ち込まない（Task 3）。

### Task 1: 提案型と根拠の検証

**Files:** `server/ai-navi/types.ts`, `server/ai-navi/output.ts`, `tests/ai-navi/output.test.ts`。
**Interfaces:** `DiscoveryHistory { id:string; materialId:string; kind:'view'|'complete'; title:string; description:string; topics:string[] }`。`Suggestion { theme:string; reason:string; firstStep:string; basis:'history'|'popularity'; evidenceIds:string[]; relatedMaterialIds:string[] }`。`validateSuggestions(value:unknown, allowedIds:ReadonlySet<string>, evidence:{ historyIds:ReadonlySet<string>; popularMaterialIds:ReadonlySet<string> }): Suggestion[]`。

- [ ] テストを先に変更。既知のhistory根拠・空の関連教材を許可、架空根拠・人気と履歴の混同・未知教材・重複テーマ・4件・空文を拒否する。各文章は1〜500文字、根拠と関連教材は各最大5件、重複ID不可。history根拠は履歴ID、popularity根拠は集計で人気の教材IDに限定する。
- [ ] `node --import tsx --test tests/ai-navi/output.test.ts` の失敗を確認。
- [ ] 型と検証を実装。根拠は1件以上。理由と最初の一歩は文字列として扱う。指定のない余分なフィールドは返さない。
- [ ] 同テストPASSを確認し、対象ファイルをローカルコミット。

### Task 2: サンプル履歴とAIのテーマ生成

**Files:** `server/ai-navi/demo-data.ts`, `server/ai-navi/openai.ts`, `tests/ai-navi/openai.test.ts`, `tests/ai-navi/demo-handler.test.ts`。
**Interfaces:** `DemoContext.history: DiscoveryHistory[]` を追加、`interests/goals/historyTopics` は廃止。`demoProfiles` は `id,label,description,history` を持つ。ケースは `photography`（写真の完了＋花飾りの閲覧）、`food-photo`（写真の完了＋料理の閲覧）、`newcomer`（履歴なし）。履歴IDにkindを含め、閲覧と完了を同じIDにしない。既存candidates/popularityは維持。

- [ ] 通信境界テストを先に変更。申告欄がなく、履歴教材内容・kind・IDが送られること、写真＋花と写真＋料理で入力が変わることを確認。根拠捏造・履歴なしのhistory根拠・未知教材をINVALID_OUTPUTにする。refusal/timeout等の既存検証を維持。ハンドラーの偽応答も新形式へ変更する。
- [ ] `node --import tsx --test tests/ai-navi/openai.test.ts` の失敗を確認。
- [ ] サンプル・プロンプト・JSON Schemaを新形式へ変更。申告ではなく履歴同士の関連性からテーマを発見し、断定しない。履歴なしでは人気根拠のみ許可。既存の空候補なら即returnは外し、関連教材なしでもテーマ生成できるようにする。渡す証拠集合で出力を検証する。
- [ ] `npm run test:ai-navi` でPASS、型チェックはTask 3の画面更新までの既知の型移行箇所を記録。対象ファイルをコミット。

### Task 3: 履歴中心の画面・統合検証

**Files:** `app/ai-navi/page.tsx`, `app/ai-navi/DemoClient.tsx`, `app/ai-navi/demo.css`, `tests/browser/ai-navi.spec.ts`, `docs/ai-navi-development.md`。
**Interfaces:** ページからサンプルのhistoryをクライアントへ渡し、ケース切替は開発者向けの履歴確認と説明。POSTは既存のprofileIdのみ。応答は新Suggestion[]。

- [ ] ブラウザテストを先に変更。希望入力なし、履歴の閲覧/完了表示、テーマが主見出し、理由・最初の行動・根拠・任意教材を表示、HTML非解釈、切替後破棄、キーなし案内を確認。
- [ ] 旧画面でブラウザテストが失敗することを確認。
- [ ] 画面を実装。「履歴から見つかる、新しい楽しみ」を主メッセージにし、希望の自由入力は設けない。履歴ケースはテスト用と明記。人気根拠と個人履歴根拠をサーバー返却のIDから照合して表示。カタログは折りたたみの補助情報にする。
- [ ] ブラウザテスト、デスクトップ/スマホ表示、`npm test`、`npx tsc --noEmit` を確認。開発サーバー停止後に `npm run build`、専用起動で表示確認。
- [ ] 開発資料・承認済み要件・設計・計画をコミット。APIキーなしの場合、実APIの意味的品質は未検証と明示する。

## Self-review

履歴入力と生成根拠はTasks1〜2、主表示・補助教材・古い結果はTask3で検証。属性推定・実プロフィール・DB保存は今回追加しない。履歴IDがkindごとに異なるため完了の捏造を集合検証で拒否できるが、自由文の意味的な断定はプロンプトと実モデル評価で確認する。
