# 独自2D交流空間 PC版 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for inline execution, or superpowers:subagent-driven-development if the user explicitly selects delegation. Implement task-by-task. Steps use checkbox syntax.

**Goal:** PCブラウザーで最大50人を目標にアバターを共有し、4室・各8人で部屋別ビデオ通話できるテスト版を作る。

**Architecture:** Next.jsの紹介ページに `/plaza` を追加し、常駐Node.jsサーバーが人数・位置・所属を管理する。通話はLiveKit Cloudと連携し、所属に限定したトークンを発行する。画面、位置共有、通話接続を分離し、後のタッチ操作追加に備える。

**Tech Stack:** 既存Next.js 14・React 18・TypeScript・Tailwind CSS、WebSocket用 `ws`、TypeScript実行とNode組込みテスト用 `tsx`、`livekit-client`、`livekit-server-sdk`。導入時に互換性を確認しpackage-lock.jsonで版を固定する。

**Spec:** `docs/metaverse-design.md`、`docs/requirements.md` 第12章。

## Global Constraints

- 全体50人、交流ルーム4室、各室8人。ロビーと切断時の予約枠も全体人数に含む。
- 同じ部屋だけで通話し、ロビーは通話なし。機器は入場・部屋変更・復帰時にオフ。
- PCテストを優先し、仮完成でスマートフォン・タブレットへ対応。VRは使わない。
- ニックネームは前後空白を除く1〜20文字。同名はIDで区別する。
- 20Hzで移動計算、10Hzで同室配信、500ms入力途絶で停止。
- 最大メッセージ4KB、毎秒30件まで。部屋変更予約10秒、異常切断の枠保持30秒。
- 機器がなくても参加できる。通話接続と在室を別表示にする。
- 公開時HTTPS・WSS・Origin許可リスト。秘密鍵を公開変数・ログへ出さない。
- アカウント・DB・録画・画面共有・文字チャット・近接通話はPC版の対象外。
- このディレクトリーはGitリポジトリーではない。無断で初期化せず、コミットは行わない。既存ファイルを尊重して差分を確認する。

## Review Focus

1. 同時入室・移動予約中の切断で人数が超過・漏洩しない（Task 2）。
2. 入場直後・トークン更新直後の退出でも旧トークンが再利用できない（Task 3・7）。
3. 遅れて返る通話接続結果が現在の部屋を上書きしない（Task 5）。
4. 権限拒否・機器切断・音声自動再生制限でも操作不能にならない（Task 4・5・7）。
5. 再接続後に古いソケット終了が新しい枠を解放しない（Task 6）。

## 基盤・環境の決定

通話はLiveKit Cloudを選ぶ。セルフホスト版は既存トークンの即時失効がないため、今回の「退室後に旧部屋へ再接続できない」条件には使用しない。

根拠：
- https://docs.livekit.io/frontends/reference/tokens-grants/
- https://docs.livekit.io/intro/basics/rooms-participants-tracks/participants/
- https://docs.livekit.io/reference/other/roomservice-api/

`RemoveParticipant` に明示的な `revoke_token_ts` を指定する。既定の時刻バッファでは直前発行のトークンが通る場合がある。秒単位の同時刻発行も拒否するよう、同一identityの発行を直列化し、発行済みnbfの最大値より後のcutoffを用いる。許容時計差を超える場合は移動を失敗扱いにする。SDKがこの引数を公開しない場合はサーバー側で公式Twirp APIの `RemoveParticipant` を呼ぶ。更新済みトークンも含め実基盤で検証するまで失効保証を完了としない。

`.env.example` に `PLAZA_PORT=3003`、`NEXT_PUBLIC_PLAZA_WS_URL=ws://localhost:3003`、`PLAZA_ALLOWED_ORIGINS`、`LIVEKIT_URL`、`LIVEKIT_API_KEY`、`LIVEKIT_API_SECRET` を説明付きで置く。NextのPC開発ポートは既存動作と同じ3002。秘密値をドキュメントに記入しない。通話設定なしでは「通話未設定」を表示し、成功を装わずロビーと共有移動を検証可能にする。

Cloudプロジェクトの作成・費用を伴う契約は別途承認対象。コード連携までは設定なしで進められる。実通話受入試験にはCloud設定と参加用実端末が必要。公開先はHTTPSのNextホスト＋WSS対応常駐Nodeホストを必要条件とし、契約先は公開工程で決定する。localhostだけでは遠隔参加はできない。

## ファイルと共有インターフェース

- `shared/plaza/types.ts`：`RoomId = 'lobby' | 'cafe' | 'hobby' | 'learning' | 'sharing'`、`AvatarId`、`Participant`、判別可能なClientMessage/ServerMessage。
- `shared/plaza/world.ts`：部屋定義、上限、出現位置、衝突領域。
- `shared/plaza/validation.ts`：`parseClientMessage(raw: string): ClientMessage | null`。
- `shared/plaza/movement.ts`：`advance(position: Position, direction: Direction, dtMs: number, room: RoomId): Position`。
- `server/plaza/state.ts`：`PlazaState` と人数・予約・接続世代管理。
- `server/plaza/calls.ts`：`CallGateway`、LiveKit実装、設定なし実装。
- `server/plaza/transitions.ts`：部屋変更の直列化と予約失効。
- `server/plaza/index.ts`：HTTPヘルスチェックとWebSocket起動。
- `app/hooks/usePlazaConnection.ts`：位置・入退室・再接続。
- `app/hooks/useRoomCall.ts`：通話状態と機器のライフサイクル。
- `app/plaza/page.tsx`、`app/components/plaza/{PlazaClient,JoinForm,WorldMap,RoomPicker,CallPanel,ConnectionStatus}.tsx`：画面。
- `tests/plaza/`：単体・接続統合テスト。`scripts/plaza-load.ts`：人数検証。
- `docs/plaza-testing.md`：起動・設定・検証記録。

## Task 1: 通信型・マップ・入力規則

**Files:** shared/plazaの4ファイル、`tests/plaza/world.test.ts`、package.json、package-lock.json、`.env.example`。
**Interfaces:** 上記共有型と `parseClientMessage`・`advance` を公開。Participantはid/name/avatar/roomId/x/y/facing/connectedを持つ。ServerMessageはwelcome/state/roomChanged/callCredentials/errorを判別する。

- [ ] 入力不正、20文字上限、未知の部屋、NaN、斜め移動、壁・境界・停止の失敗テストを書く。`advance(p,{x:1,y:1},100,'lobby')` の距離が単軸と同じになることを確認する。
- [ ] `tsx` と `ws` の必要な依存と型を導入し、`test:plaza` = `tsx --test tests/plaza/*.test.ts` を追加。`npm run test:plaza` が未実装で失敗することを確認。
- [ ] 共有型・部屋定義・検証・移動を実装。原点や出現位置は各マップ内に固定し、速度は毎秒120ワールド単位とする。
- [ ] `npm run test:plaza` と `npx tsc --noEmit` が成功することを確認する。

## Task 2: 人数管理と実WebSocket共有

**Files:** server/plaza/state.ts、server/plaza/index.ts、`tests/plaza/state.test.ts`、`tests/plaza/socket.test.ts`、package.json。
**Interfaces:** `PlazaState.join(input: JoinInput): JoinResult`、`reserveRoom(id: string, target: RoomId, requestId: string): ReservationResult`、`commitRoom(requestId: string): Participant`、`cancelReservation(requestId: string): void`。予約中は元枠と移動先枠を維持するが全体人数を二重計上しない。

- [ ] 50人の後の51人目拒否、同時9件入室で8件だけ成功、予約期限10秒、切断で予約解放、全体人数二重計上なしの失敗テストを書く。
- [ ] `npm run test:plaza` で新しいテストの失敗を確認。
- [ ] サーバーを実装し、`dev:plaza` = `tsx server/plaza/index.ts` を追加。`/health` を提供、Origin検証・4KB・毎秒30件制限と10Hz同室配信を実装。起動時の設定不正は明示的に失敗する。
- [ ] テストサーバーを空きポートで起動する統合テストで2接続の移動共有、別部屋への位置情報非配信、不正入力拒否を確認。全テストと型検査を通す。

## Task 3: 通話権限と部屋変更

**Files:** server/plaza/calls.ts、server/plaza/transitions.ts、`tests/plaza/transitions.test.ts`、`.env.example`、package.json/lock。
**Interfaces:** `CallGateway.issue(participant: Participant, generation: number): Promise<CallCredentials>`、`revoke(roomId: RoomId, identity: string): Promise<void>`。`CallCredentials` はurl/token/identity/roomId/generation。identityは参加者ID＋接続世代＋部屋訪問連番。`changeRoom(id: string, target: RoomId, requestId: string): Promise<RoomChangeResult>` を公開する。

- [ ] FakeGatewayで「予約→旧通話失効→所属確定→新資格発行」の順、失効失敗時は所属維持、新資格発行失敗時はロビー、同時変更拒否、期限切れ後の遅延結果破棄、ロビーで資格を発行しない失敗テストを書く。
- [ ] `npm run test:plaza` で失敗を確認。
- [ ] SDKを導入し、公式型を確認して実装。トークンは有効期間60秒・現在の部屋限定・camera/microphoneのみ・データ公開不可・管理権限なし。旧訪問identityを失効させてから新訪問identityを生成する。SDK・APIのエラーは成功として処理しない。
- [ ] 設定なしGatewayは明示的なCALL_UNCONFIGUREDを返す。秘密値とトークンをログに出さない。FakeGatewayで全テストを通し、Cloud実検証をTask 7に残す。

## Task 4: PC入場・ロビー・部屋UI

**Files:** app/plaza/page.tsx、PlazaClient/JoinForm/WorldMap/RoomPicker/ConnectionStatus、usePlazaConnection、Header.tsx、HeroSection.tsx。
**Interfaces:** `usePlazaConnection(): {status,self,participants,rooms,error,join,move,changeRoom,leave}`。入力は共有型、状態は未入場/接続中/参加中/再接続中/満員。WorldMapはpositionの表示のみを担当する。

- [ ] `tests/plaza/client-state.test.ts` にwelcome前は移動不可、古い世代のstateを無視、満員後に再試行可能という状態遷移テストを追加し、失敗を確認。
- [ ] 接続状態の純粋な更新関数を `app/components/plaza/client-state.ts` に実装し、テストを通す。
- [ ] 画面・フックを実装。任意の機器確認はクリック時だけ行い、確認用tracksを必ず停止。人物SVG、名前、部屋人数、現在地、退出を表示。名前入力中・blur・通信断で移動停止。部屋一覧はキーボード対応。
- [ ] ヘッダーとヒーローの交流導線を `/plaza` に接続する。
- [ ] 型検査・ビルドを通し、2タブで名前・移動・部屋変更・満員表示を確認。権限拒否でも入場でき、確認終了後にカメラが停止することを実端末で確認する。

## Task 5: 通話UIと機器停止

**Files:** useRoomCall、CallPanel、`app/components/plaza/call-state.ts`、`tests/plaza/call-state.test.ts`、package.json/lock。
**Interfaces:** `useRoomCall(credentials: CallCredentials | null): {status,error,participants,micEnabled,cameraEnabled,toggleMic,toggleCamera,disconnect}`。通話世代が変わる時は以前のRoomを切断してtracksを停止する。

- [ ] 接続中に部屋変更した場合の旧完了結果破棄、機器オン失敗で表示がオンにならない、退出・復帰・アンマウントで機器がオフになる失敗テストをFakeRoomで書く。
- [ ] `npm run test:plaza` で失敗を確認。
- [ ] `livekit-client` を導入し、純粋な状態遷移と接続管理を実装。接続時には自動公開しない。映像・音声tracksを要素へattachし、解除時detachする。音声の自動再生が拒否された場合「音声を開始」を表示する。
- [ ] 同じ部屋の映像・名前・機器状態と接続失敗の再試行を表示。Gateway設定なしでも空間を使え、通話未設定と表示する。
- [ ] FakeRoomの試験、型検査、ビルドを通す。Cloudでの実音声・映像はTask 7で確認する。

## Task 6: 再接続と資源解放

**Files:** state.ts、index.ts、transitions.ts、usePlazaConnection、useRoomCall、`tests/plaza/reconnect.test.ts`。
**Interfaces:** `disconnect(id: string, generation: number, nowMs: number): void` と `resume(token: string, nowMs: number): JoinResult`。サーバー再起動で未知のトークンは新規入場処理に戻す。

- [ ] 30秒以内復帰で同ID・所属・位置維持、30秒後は定員再確認、古いソケットcloseで新枠が消えない、予約中切断、通話失効API失敗時に新資格を渡さない試験を追加し失敗を確認。
- [ ] 10秒疎通確認・20秒無応答判定・30秒猶予、1/2/4/8秒再試行、sessionStorageのトークン保持を実装。明示的退出では再試行を停止しトークンを破棄する。
- [ ] 元通話失効が確認できない復帰では通話再接続を止めて復旧案内する。起動時に専用room prefixの残存接続を整理し、整理完了前は資格発行しない。他の用途のCloud roomを削除しない。
- [ ] 全テストと型検査を通す。2タブでサーバー再起動・ネットワーク断・タブ終了・再入場を確認する。

## Task 7: 人数・通話・画面の受入検証

**Files:** scripts/plaza-load.ts、`docs/plaza-testing.md`、package.json、requirements/designの状態欄。
**Interfaces:** `npm run load:plaza -- --clients 30` および `--clients 50`。各試験は位置共有、部屋変更、51人目と各室9人目の拒否を計測し、失敗時は非ゼロ終了する。

- [ ] 負荷スクリプトを実装し、30・50接続の結果と遅延を記録。トークン・名前・音声映像を記録しない。
- [ ] `npm run test:plaza`、`npx tsc --noEmit`、`npm run build` を実行。開発サーバーとビルドで同じ `.next` を同時使用しないよう停止・再起動を管理する。
- [ ] PC画面で入場から退出まで検証。ブラウザスキルのアプリ内ブラウザを優先し、接続不可ならユーザーの通常ブラウザで確認する。HTTP 200だけで画面確認済みとしない。
- [ ] Cloud設定後、2実端末の同室通話・別室隔離、入場直後とトークン更新直後の退出・旧資格再利用拒否を確認。cutoff境界・退出済み参加者・API障害も検証する。
- [ ] 各室8人、4室並行通話を関係者の端末で試験し、人数・端末・ブラウザ・ネットワーク・時間・途切れ・CPU・帯域を記録。できなかった試験は未実施と明記する。
- [ ] 起動手順、環境変数、未設定時の挙動、設定方法、PCテスト結果、公開前の残作業を文書化。実装済み・自動試験済み・実通話確認済みを分けて報告する。

## 次の段階：仮完成時のスマートフォン・タブレット

PC版の検証結果を踏まえ、タッチ方向操作、通話パネルの折りたたみ、画面回転、ソフトキーボード、背景復帰を別の実装計画で追加する。今回のPC版完了をモバイル対応完了とは扱わない。

## 自己レビュー

設計の人数、部屋隔離、機器停止、復帰、検証はTask 1〜7へ対応付け済み。通話失効のCloud依存と実端末試験を明示した。Git初期化、Cloud契約、一般公開は行わない。実装前にユーザーが計画をレビューし、実行方式を選択する。


## 2026-10-06 実行結果

PC版の画面、位置共有、部屋変更、通話連携、再接続を実装。Node34件、Chrome2件、30/50自動接続を確認。独立レビューで見つかった非同期競合、キー反復、復帰案内、出現位置を修正。

Task 7の実映像・音声、Cloud失効、各室8人・4室並行、遠隔接続は接続設定・実端末を用意してから実施する。実装済みと実通話検証済みを区別する。詳細は `docs/plaza-testing.md`。
