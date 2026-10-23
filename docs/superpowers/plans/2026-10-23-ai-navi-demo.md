# AI未来ナビ開発体験版 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** サンプル利用者でOpenAIによる提案を試せる開発用画面を作る。
**Architecture:** 固定サンプルを推薦コアに通し、サーバー専用アダプターがResponses APIへ送る。開発専用ルートはローカルOriginを検証し、キー・任意プロフィール・全体履歴をブラウザへ出さない。
**Tech Stack:** Next.js 14、React 18、TypeScript、組み込みfetch、node:test、Playwright。新規依存なし。
**Spec:** `docs/superpowers/specs/2026-10-23-ai-navi-demo-design.md`

## Global Constraints

- 開発環境・ループバックOriginだけ。非開発環境では404。
- サンプルIDのみ受付。最大3提案、候補外ID拒否。
- `OPENAI_API_KEY` と `OPENAI_MODEL` はサーバー環境変数。20秒タイムアウト、自動再試行なし、store:false。
- 1プロセス60秒3要求、実行中は重複送信しない。
- 人気・履歴はサンプルと明示。本番認証・永続履歴は接続しない。

## Review Focus

- OpenAIが200でもrefusal/incomplete：成功扱いしない（Task 1）。
- AI出力内のHTML：テキスト表示のみ（Task 3）。
- 長大・未知の入力：OpenAIへ送信しない（Task 2）。
- 同じOriginの偽装や公開Host：ループバックHostと完全Origin一致を要求（Task 2）。
- プロフィール切替中の古い応答：表示を破棄する（Task 3）。

### Task 1: サンプルとOpenAIアダプター

**Files:** Create `server/ai-navi/demo-data.ts`, `server/ai-navi/openai.ts`, `tests/ai-navi/openai.test.ts`。
**Interfaces:** `demoProfiles`（id,label,description,personal）、`demoMaterials: Material[]`、`createDemoContext(profileId: string, now: number)` → 候補教材、本人の関心・目標、個人IDを含まない履歴分野、集計と候補根拠。`generateSuggestions(context: DemoContext, config: { apiKey:string; model:string }, fetcher?: typeof fetch): Promise<Suggestion[]>`。`AiNaviError` はcode（UNCONFIGURED/TIMEOUT/UPSTREAM/INVALID_OUTPUT/REFUSED）を保持する。

- [x] 通信境界だけを差し替えるテストを先に書く。固定OpenAI URL・Bearer・store:false・構造化schema、名前/他者ID未送信、候補外ID拒否、キーなし、200の拒否/未完了/不正JSON、HTTP失敗とAbortを検証。
- [x] `node --import tsx --test tests/ai-navi/openai.test.ts` が未実装で失敗することを確認。
- [x] サンプルとアダプターを実装。サンプルは写真・生花・料理など5件、利用者5人中4人が閲覧する教材を含む。「写真」と「履歴なし」の2プロフィール。OpenAIへは候補教材と根拠だけ渡す。Responses APIのoutput message内のoutput_textだけを解析し、refusal/incompleteを区別。200でも不正なら失敗。タイマーはfinallyで解除する。
- [x] 同テストと `npm run test:ai-navi` でPASSを確認。
- [x] タスクの対象ファイルをコミット。

### Task 2: 開発専用API

**Files:** Create `server/ai-navi/demo-handler.ts`, `app/api/ai-navi/demo/route.ts`, `tests/ai-navi/demo-handler.test.ts`。Modify `.env.example`。
**Interfaces:** `createDemoHandler(options: { environment:string; config:{ apiKey:string; model:string }; generate?: typeof generateSuggestions; now?:()=>number }): (request:Request)=>Promise<Response>`。ルートはPOSTのみを接続、Node.js runtime。ページも使う `isLocalDemoUrl(url:string): boolean` をhandlerで公開する。

- [x] 非開発404、外部Host/Originなし/Origin不一致403、JSON以外415、1KB超413、不正JSON/未知ID400、キーなし503、60秒3回以後429、同時実行429、時刻経過後の回復、上流エラーの秘密情報非公開をテストする。
- [x] `node --import tsx --test tests/ai-navi/demo-handler.test.ts` の失敗を確認。
- [x] ハンドラー実装。Hostはlocalhost・127.0.0.1・[::1]のみ、Originはrequest.url.originと完全一致。リクエストボディはストリームで最大1024バイト読み、超過したらキャンセルする。固定IDを検証してから外部呼出し。レスポンスはno-store。設定・Origin・本文エラーで上限枠を消費しない。時間はサーバーで生成。
- [x] テストPASS、型チェック成功を確認。`.env.example` に空のキー・モデルを追加。実キーを読んだりコピーしたりしない。
- [x] タスクの対象ファイルをコミット。

### Task 3: 開発用操作画面と検証

**Files:** Create `app/ai-navi/page.tsx`, `app/ai-navi/DemoClient.tsx`, `app/ai-navi/demo.css`, `tests/browser/ai-navi.spec.ts`。Modify `docs/ai-navi-development.md`。
**Interfaces:** ページが開発環境とローカルHostを確認してクライアントを表示。クライアントはサンプル選択、明示実行、loading、エラー、提案を表示。POST bodyは `{ profileId }` のみ。

- [x] ブラウザテストを先に書く。キーなしの設定案内、ルート通信を差し替えた提案表示とHTML非解釈、実行中の重複防止、選択切替後の古い応答破棄を検証する。
- [x] 画面なしでテストが失敗することを確認。
- [x] 画面を実装。既存紹介セクションを変更せず `/ai-navi` を直リンクで使用。サンプル教材一覧・サンプル人気の割合/人数/期間も表示。教材はサンプル説明を画面内に表示し存在しない教材URLへリンクしない。入力切替でAbortし古い応答を無視する。
- [x] Playwrightで新規画面を検証し、デスクトップ・スマホのスクリーンショットを確認。`npm test`、`npx tsc --noEmit`、`npm run build` を確認。ビルドされた本番サーバーではページとAPIが404になることを確認する。
- [x] 開発資料に `npm run dev`、`/ai-navi`、環境変数の設定・サーバー再起動、未実施の実API確認、費用が発生する操作を明記し、コミットする。

## セルフレビュー

入力・出力・開発限定・費用制御はTasks 1〜2、サンプル表示・失敗案内・古い応答はTask 3に対応。本人認証と本番保存は意図的に範囲外。サンプルの日時はサーバー現在時刻から生成するため30日集計が経時で空にならない。実APIの検証はキー設定後のみ。

## 実行結果と最終レビュー

2026-10-23：3タスク実装。初回レビューのImportant（Host/OriginだけではLAN接続元を判別できない）を回帰テストで再現後に修正。専用 `npm run dev:ai-navi` は127.0.0.1:3002へ固定し、通常dev・LANでは有効化しない。追加の起動引数を拒否。既存51件とAI関連27件、ブラウザ3件が成功。キーは空で検証したため、実APIの提案取得・品質は未検証。NextRequestのIP正規化の軽微な制約は保留し、URLはlocalhostを案内する。
