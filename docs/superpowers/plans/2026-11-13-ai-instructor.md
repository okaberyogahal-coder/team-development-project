# AI講師ローカル検証版 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** サンプル教材1本で、本文を根拠に質問し、固定問題と復習まで体験できるAI講師をローカルで提供する。

**Architecture:** 教材・正答はサーバー管理とし、ブラウザには公開教材と回答済み問題の判定だけを渡す。会話は既存ローカルログインのトークンに結び付けたメモリストアで管理し、専用APIを通じてAIに接続する。未来ナビの推薦処理・DBスキーマは変更しない。

**Tech Stack:** Next.js 16.4.2 / React 19.3.1 / TypeScript / Node test runner / Playwright。新規依存は追加しない。

**Spec:** `docs/superpowers/specs/2026-11-13-ai-instructor-design.md`

## Global Constraints

- 開発環境・ローカルホスト・専用有効化設定・同一オリジンを要求する。認証は既存ローカルセッションを使う。
- 初期教材は「自然光と背景ぼけの基本」。サンプルであることを明記し、本文・学習目標・安定した節ID・版・三択問題3問を持つ。
- 質問1件1,000文字、会話は直近10メッセージ、1セッション20回、1分3回、同時要求1件、教材本文12,000文字、回答出力1,200トークン、タイムアウト20秒。
- 無操作30分・ログアウト・再開始で会話を失効する。保持中会話は最大100件。会話本文を永続ストア・通常ログに記録しない。
- 氏名・メール・年齢・他教材の履歴をAIに追加しない。本文・質問の提供先を利用前に説明する。
- 文字チャットのみ。音声・アバター・アップロード・DB変更・正式な成績記録・本番公開は含めない。
- 実装前に対象の `node_modules/next/dist/docs/` を読む。計画時点で route handlers と use client のガイドは確認済み。
- 無関係な未追跡ファイルは変更しない。コミット操作は現在の作業領域・権限に従い、必要な場合は作業ファイルだけを対象にする。

## Review Focus

- 日本語1,000文字が既存の1KB入力上限で拒否されないこと（Task 3）。
- 同じユーザーの別ログインからも他セッションの会話へアクセスできないこと（Task 2 / 3）。
- 再開始で20回制限を回避できず、通信失敗の連打も制限されること（Task 2 / 3）。
- 非表示タブでログアウトした後、再表示時に古い会話が残らないこと（Task 4）。
- 固定問題の正答がRSC payload・初期API応答・ブラウザバンドルから漏れないこと（Task 1 / 4）。

---

### Task 1: 教材と固定確認問題

**Files:** Create `shared/ai-instructor.ts`, `server/ai-instructor/material.ts`, `tests/ai-instructor/material.test.ts`.

**Interfaces:**
- `shared/ai-instructor.ts`: `PublicLesson`（id, version, title, goals, sections: {id,title,body}[], sample: true）,`PublicQuestion`（id,prompt,choices:string[]）,`TutorReply`（kind: 'answer' | 'out_of_scope', text, sectionIds:string[]）。正答型・値は共有モジュールに置かない。
- `material.ts`: `getPublicLesson(id:string):PublicLesson|null`, `getPublicQuestions(id:string):PublicQuestion[]`, `gradeQuestion(materialId:string, questionId:string, choice:number):{correct:boolean,explanation:string,sectionId:string}`。未知のID・不正な選択肢は例外。

- [x] 固定3問、各3択、本文12,000文字以内、節IDと復習先の実在、3問すべての全選択肢の判定、公開データに正答・解説が含まれないことをテストする。
- [x] `node --import tsx --test tests/ai-instructor/material.test.ts` を実行し、未実装による失敗を確認する。
- [x] 自然光・被写体と背景の距離・背景整理を節に分け、一般的な写真の基本に限定した本文と固定問題を実装する。新規サンプル本文である旨を明記する。サーバー専用の入口で教材を読み、クライアントは共有型のみを参照する。
- [x] 同じコマンドで全件PASSを確認する。

### Task 2: 会話ストアとAI応答検証

**Files:** Create `server/ai-instructor/store.ts`, `server/ai-instructor/output.ts`, `server/ai-instructor/openai.ts`, `tests/ai-instructor/store.test.ts`, `tests/ai-instructor/openai.test.ts`.

**Interfaces:**
- `InstructorStore` は `start(ownerToken, lesson, now)`、`get(ownerToken, conversationId, now)`、`beginQuestion(ownerToken, conversationId, sequence, now)`、`finishQuestion(lease, question, reply, now)`、`cancelQuestion(lease)`、`revokeOwner(ownerToken)` を持つ。型は同ファイルからexport。会話はランダムID・教材版・単調増加sequence・最終操作時刻・問題回答結果を持つ。
- `beginQuestion` は同期的に所有権・版・sequence・上限を検証してロックし、server-managed messagesとleaseを返す。期限切れ・再開始・認証失効後のleaseはコミットできない。
- `validateTutorReply(value:unknown,lesson:PublicLesson):TutorReply` は長さ、kind、根拠IDを検証する。answerには根拠1件以上、out_of_scopeは根拠なし。
- `generateTutorReply(input:{lesson:PublicLesson,messages:{role:'user'|'assistant',content:string}[],question:string},config:{apiKey:string,model:string},fetcher?:typeof fetch):Promise<TutorReply>`。

- [x] 30分境界、100件上限、別トークン拒否、連番競合、同時1件、60秒3件・累計20件、再開始後も上限継続、古いlease拒否をfake clockでテストし、未実装でFAILを確認する。
- [x] メモリストアを実装する。回数は会話でなく所有ログイン単位に保持し、失敗試行も数える。再開始で旧会話を削除、会話は直近10件へ切り詰める。期限切れデータは操作時に掃除し、定期掃除用メソッドを用意する。ログイン制限用メタデータも期限切れで削除する。
- [x] AI未設定、HTTP失敗、20秒タイムアウト、refusal、JSON不正、過長回答、未知の根拠IDをfetch mockでテストし、未実装でFAILを確認する。
- [x] 既存の接続方式を参考に専用アダプターを実装する。OpenAI利用の実装前にopenai-docsスキルで公式情報を確認する。既存OPENAI_API_KEY / OPENAI_MODELを使い、教材を根拠とする日本語回答、例示、教材外応答、入力中の命令を無視する指示を設定する。store:false、出力上限1,200、20秒でabort。provider errorや質問本文をログに出さない。
- [x] `node --import tsx --test tests/ai-instructor/{store,openai}.test.ts` で全件PASSを確認する。

### Task 3: 認証付きAPIと失効連携

**Files:** Create `server/ai-instructor/handler.ts`, `server/ai-instructor/http.ts`, `app/api/ai-instructor/route.ts`, `tests/ai-instructor/handler.test.ts`. Modify `server/local-demo/session.ts`.

**Interfaces:**
- `createInstructorHandler({environment,enabled,sessions,store,config,generate,now})` はGET/POSTハンドラーを返す。依存はテストで差し替え可能。
- GET: 認証済みの公開教材・公開問題とAI設定有無のみ返す。
- POSTの厳密な判別型：`{action:'start',materialId}` / `{action:'question',conversationId,sequence,question,sectionId,mode:'question'|'simple'|'example'}` / `{action:'answer',conversationId,sequence,questionId,choice}` / `{action:'reset',conversationId}`。
- 会話応答はconversationId、sequence、公開会話、回答済み判定、残り回数、historyTruncatedを含む。クライアントから履歴・本文・正答は受け取らない。
- `LocalSessionStore.onRevoke(listener:(token:string)=>void):()=>void` を追加し、明示revoke・期限切れ・上限による追い出し時に通知する。AI講師ストアはプロセスで一度だけ購読し、revokeOwnerで会話を削除する。

- [x] 非ローカル・本番・無効設定・未認証・Origin不一致・GETへの正答混入・他所有者アクセス・未知の節・問題ID・版変更をテストする。
- [x] 日本語1,000文字の受理、1,001文字・16KB超の拒否、JSON不正、余分なキー・クライアント履歴の拒否をテストする。AI実行中のlogout/reset/版変更で応答を破棄し、ロック解放・失敗の回数制限も確認する。未実装のFAILを確認する。
- [x] 既存guardLocalRequest、getSessionToken、localJSONを再利用する。新規http.tsでストリームを16KBまで読む。既存1KBリーダーは変更しない。質問の文字数はUnicodeコードポイントで数える。
- [x] APIの各操作前とAI応答後に認証・所有トークン・版・leaseを再確認する。問題回答もsequenceを更新し、正答開示は回答した問題のみにする。設定不足503、上限429、期限切れ410、競合409、認証401、上流502、タイムアウト504、入力400/413/415を日本語で区別する。
- [x] 起動設定 `MILIFE_AI_INSTRUCTOR_DEMO=local` を要求し、nodejs runtime / no-storeで接続する。会話の定期掃除を単一プロセスで重複しないよう登録する。
- [x] `node --import tsx --test tests/ai-instructor/*.test.ts tests/local-demo/*.test.ts` のPASSを確認する。

### Task 4: 学習体験・起動方法・ブラウザ検証

**Files:** Create `app/ai-instructor/page.tsx`, `app/ai-instructor/InstructorClient.tsx`, `app/ai-instructor/instructor.css`, `scripts/dev-ai-instructor.mjs`, `playwright.ai-instructor.config.ts`, `tests/browser/ai-instructor.spec.ts`, `docs/ai-instructor.md`. Modify `package.json`, `.env.example`.

**Interfaces:**
- `/ai-instructor` は開発・ループバック・専用設定を検査するサーバーページ。秘密や正答をpropsに渡さない。
- `InstructorClient` はTask 3のAPIのみ利用。既存 `/ai-navi` のローカルログインで認証し、未ログイン時はそこへ案内する。
- `npm run dev:ai-instructor` は127.0.0.1:3003固定でNext devを起動し、未来ナビとAI講師両方のローカル設定を有効化する。追加引数を拒否し、LANには公開しない。
- `npm run test:ai-instructor` は新規unit suite、`npm run test:ai-instructor:browser` は専用Playwright設定を実行する。

- [x] ブラウザ試験を先に追加する。本文→同意説明→開始→質問→根拠節へ移動→3問回答→復習のフロー、未設定時も本文と問題が使えることを確認する。
- [x] 別タブlogout後のfocus/visibilitychangeでの会話消去、通信失敗と二重送信、再開始中の遅延応答破棄、320px幅とキーボード操作をテストする。UIのAI成功経路はAPI interceptを使い、実AI検証とは区別する。未実装のFAILを確認する。
- [x] 本文・質問・固定問題を区切って表示し、節選択、やさしい説明、具体例、質問、回答中表示、復習を実装する。回答はプレーンテキストで表示する。AbortControllerとrequest generationで古い応答を除外し、表示復帰・操作前に認証確認する。履歴省略・再起動による消失・試験教材の表示を追加する。
- [x] 専用Playwrightは開発サーバーと独立した一時fixtureを使う。初期HTML/RSC、初期API、クライアント生成物に正答データがないことを確認する。監修済み教材のように表示しない。
- [x] 起動手順・ローカルログイン・サーバー設定・制限・メモリ保持・本番未対応・既存未来ナビへの影響範囲をdocsに記載する。秘密の値は読み出し・記録しない。
- [x] `npm run test:ai-instructor`、`npm run test:local-demo`、`npm run test:ai-navi`、`npm run test:ai-instructor:browser`、`npx tsc --noEmit`、`npm run lint`、`npm run build` を実行する。失敗は今回の変更か既存問題か切り分け、未実行チェックを明記する。
- [ ] 設定済み環境でのみ実AIによる本文内質問・教材外質問・命令注入の手動検証を行う。キー未設定なら未検証と記録し、実AI動作済みとは報告しない。
- [x] 完了前にverification-before-completionとrequesting-code-reviewスキルを適用し、差分・検証結果・残る制限を確認する。

## 計画セルフレビュー

教材と問題（Task 1）、会話・根拠・制限（Task 2）、認証・失効・異常系（Task 3）、学習操作・アクセス性・起動と検証（Task 4）で設計の初期範囲をカバーする。型は共有DTOとサーバー専用型を分け、会話開始・質問・問題回答に同じsequenceを用いる。Review Focusの5条件は各タスクの試験に割り当て済み。本番移行の要件は今回の実装対象に含めない。

## 実行方法

同一セッションで本体エージェントが順番に実装するNative方式を推奨する。教材・会話・API・画面の依存が強く、並列実装の利益が小さいため。適用スキルに従い、計画のレビューと実行方法の確認後に着手する。

実行結果：コードと自動検証は完了。実AI確認は提供先の401 / expired_secret_keyにより未完了。詳細は `docs/superpowers/reports/2026-11-13-ai-instructor-verification.md` を参照。
