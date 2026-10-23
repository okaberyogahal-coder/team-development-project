# AI未来ナビ推薦コア Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans or superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 履歴と直近30日の利用率80%以上による人気を使う、検証可能な推薦コアを作る。

**Architecture:** サーバーで実行する純粋なTypeScriptモジュールとして、人気集計・候補選択・AI出力検証を分離する。認証・DB・AI提供先から独立した入力契約を固定し、サンプルを本番実績と混同しない。今回は設計書の実装順序1を対象とする。

**Tech Stack:** 既存のTypeScript、tsx、node:test。追加依存なし。

**Spec:** `docs/superpowers/specs/2026-10-22-ai-navi-design.md`

## Global Constraints

- 人気の分母は直近30日に公開教材を閲覧または受講したユニーク利用者数。同じ人の閲覧と受講は二重計上しない。
- 利用率80%以上を人気とする。分母0件では算出せず、割合・対象人数・期間を保持する。
- 個別の履歴・関心を優先。属性だけで候補を除外せず、名前・メール・他者履歴をAIへ送らない。
- 公開教材の実IDのみ。閲覧を受講完了とみなさない。
- サンプル検証を本番利用・実AI接続の完成と表現しない。

## Review Focus

- 境界時刻・未来日時：集計対象から意図どおり除外される（Task 1）。
- 同一利用者の閲覧・完了混在：分母・分子が重複しない（Task 1）。
- 閲覧後の教材非公開：候補にも人気の対象にも残らない（Task 1・2）。
- 反応撤回：古い否定が次の候補を抑制しない（Task 2）。
- AIが未知ID・重複・命令文を返す：不正構造・未知候補は拒否し、文章を実行しない（Task 3）。

## ファイルと共有インターフェース

- `server/ai-navi/types.ts`：型のみ。
- `server/ai-navi/popularity.ts`：人気集計。
- `server/ai-navi/candidates.ts`：本人の入力に基づく候補と根拠。
- `server/ai-navi/output.ts`：AI返却値の検証。
- `tests/ai-navi/*.test.ts`：node:testによる検証。
- `package.json`：`test:ai-navi` を追加し、既存 `test` に追加。広場のテストは維持。

型契約：`Material { id: string; title: string; description: string; topics: string[]; published: boolean }`、`LearningEvent { userId: string; materialId: string; kind: 'view' | 'complete'; at: number }`、`Feedback { materialId: string; value: 'interested' | 'not-interested' }`。時刻はUTC epoch milliseconds。本人用入力 `PersonalInput { interests: string[]; goals: string[]; events: LearningEvent[]; feedback: Feedback[] }` は保存基盤が本人だけに絞り、最新の反応だけを渡す契約。個別推薦に年齢・性別を追加する段階は許可UIと一緒に後続計画で扱う。

### Task 1: 人気集計

**Files:** Create `types.ts`, `popularity.ts`, `tests/ai-navi/popularity.test.ts`、Modify `package.json`。

**Interfaces:** `aggregatePopularity(materials: Material[], events: LearningEvent[], now: number): PopularitySummary`。`PopularitySummary { start: number; end: number; activeUsers: number; items: PopularityItem[] }`、`PopularityItem { materialId: string; users: number; completedUsers: number; ratio: number | null; popular: boolean }`。集計期間は `(now - 30 * 86400000, now]`。

- [ ] 失敗テストを書く。5人中4人は人気、5人中3人は非該当。閲覧・完了重複は1人。0人ではratio=null。境界時刻と未来時刻は除外。未存在・非公開教材のイベントは分母にも含めない。
- [ ] `npx tsx --test tests/ai-navi/popularity.test.ts` を実行し、未実装による失敗を確認する。
- [ ] 型と集計を実装する。整数比較 `users * 100 >= activeUsers * 80` で判定し、丸めによる誤判定を防ぐ。完了人数、利用人数、IDの順で安定ソートする。
- [ ] 同コマンドで全件PASSを確認し、`test:ai-navi` を登録する。
- [ ] 対象ファイルのみをコミットする。

### Task 2: 本人の候補選択

**Files:** Create `candidates.ts`, `tests/ai-navi/candidates.test.ts`。

**Interfaces:** `selectCandidates(materials: Material[], personal: PersonalInput, popularity: PopularitySummary): Candidate[]`。`Candidate { materialId: string; sources: ('interest' | 'history' | 'feedback' | 'popularity' | 'catalog')[] }`。AIへ渡す候補は最大20件とし、本人のイベントを他者の集計イベントとは別引数に保つ。

- [ ] 失敗テストを書く。登録した関心・目標と教材topicsの一致、本人が閲覧・完了した教材のtopicsとの一致を人気より優先。関連なしの初回は人気を優先。人気なしならcatalog根拠。非公開は除外。not-interestedは除外し、反応を取り除いた次回入力では再び候補にできる。履歴を変更しない。
- [ ] `npx tsx --test tests/ai-navi/candidates.test.ts` で失敗を確認する。
- [ ] 実装する。文字列はNFKC・前後空白除去・小文字化して比較し、相互の包含一致で判定する。interested、関心・目標一致、履歴一致、人気、一般候補の優先順とし、同順位は集計順位・IDで安定させる。これはAI前の候補抽出であり、AIによる意味理解を代替しない。
- [ ] 同コマンドと `npm run test:ai-navi` でPASSを確認する。
- [ ] 対象ファイルのみをコミットする。

### Task 3: AI出力の境界と接続契約

**Files:** Create `output.ts`, `tests/ai-navi/output.test.ts`, `docs/ai-navi-development.md`。

**Interfaces:** `validateSuggestions(value: unknown, allowedIds: ReadonlySet<string>): Suggestion[]`、`Suggestion { materialId: string; reason: string; nextLearning: string }`。入力は配列で最大3件。各文字列は空白除去後1〜500文字、materialIdは許可集合に含み重複不可。不正入力は例外で拒否する。

- [ ] 失敗テストを書く。正常値、未知ID、重複、4件、空文字、長過ぎる文章、不正JSON相当の値を検証。命令文を含む文字列は実行・HTML化せずただのデータとして返す。
- [ ] `npx tsx --test tests/ai-navi/output.test.ts` で失敗を確認する。
- [ ] 検証関数を実装し、開発資料に本人認証・入力版確認・公開状態再取得はAPI側の責務と記す。allowedIdsは応答時の最新公開教材で再作成する契約とする。
- [ ] 全体の `npm test` と `npx tsc --noEmit` を実行し成功を確認する。失敗が既存由来なら切り分けて記録する。
- [ ] 対象ファイルのみをコミットする。

## 後続計画と完了の境界

この計画の完了は推薦コアの完成であり、利用者が使えるAI未来ナビ全体の完成ではない。次は本人認証・教材カタログ・受講閲覧履歴・反応の永続保存を設計し、その後にAI提供先・モデル・費用上限・利用許可UI・提案API・画面を接続する。保存期間・退会削除・性別項目の仕様はその段階の設計で確定する。実AIの外部接続に必要なAPIキーはリポジトリへ保存しない。

## セルフレビュー

第1段階の人気・候補・出力検証をTasks 1〜3で網羅。データ保存・AIアダプター・画面・属性利用は後続であり未実装と明示した。型と各タスクのインターフェース、Review Focusの検証先を照合済み。
