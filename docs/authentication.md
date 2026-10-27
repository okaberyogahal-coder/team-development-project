# サインアップ・サインイン

既存のPostgreSQL上でBetter Auth 1.7.11を使用する。Webの登録・ログイン画面は実DBに接続済み。

## できること

- メール・パスワードで登録し、表示名・アバター・興味・教えたいこと・学びたいこと・得意なこと・目標を保存する。
- メールを小文字化し、一意のアカウントを作成。パスワードは12〜128文字、Better Authのハッシュ処理に任せる。平文をDB・ログへ記録しない。
- 認証情報・プロフィール・学びの設定・確認記録を1つのDBトランザクションで保存する。途中で失敗した場合はまとめて取り消し、成功後にログインCookieを発行する。
- `/account` で本人の情報を表示。再読込・再ログイン後も設定を保持する。
- セッションはDB管理、CookieはHttpOnly・SameSite=Lax、HTTPSではSecure。有効期間7日、更新間隔1日。ログアウトでサーバーのセッションも削除し、コピー済みCookieを再利用できなくする。
- 停止中・論理削除済みアカウントはログインやマイページ閲覧を拒否。本人データはキャッシュしない。
- `/plaza` のUnityへ本人の表示名・アバター・教えたいこと・学びたいことを反映する。未ログインはゲスト見学を維持する。

## 現時点の範囲

保護者確認の採否は2026-10-20の利用者の発言により再検討中。認証実装の前提にはしない。既存の18歳以上の自己申告チェックは開発版で当面維持し、認証ライブラリとは別の入力規則で検証する。保護者の連絡先や確認フローは実装・収集しない。

メール送信・メール所有者確認・パスワード再設定は未実装。登録者は `PENDING` / `emailVerified=false` のまま、ログインして本人のマイページとUnityの本人表示を利用できる。予約等の `ACTIVE` を要求する制約は緩めていない。共有移動・実通話、教材・予約の操作APIには未接続。

画面のチェックは開発版の利用条件、保存する情報、成人自己申告の確認。`PolicyConsent` に `development-use` / `account-data-storage` / `adult-self-declaration` と版を記録する。正式な利用規約への同意や年齢・本人確認が済んだと扱わない。公開前の規約整備・メール確認・未成年の利用条件は別途決める。

## ローカル起動

起動前に [Supabase接続・起動手順](./supabase.md#start) に従い、アプリ用の接続文字列とCA証明書のパスを `.env.local` に設定する。

```powershell
npm.cmd ci
# 初回のみ .env.example を参考に .env.local を作成。既存設定は保持する。
npm.cmd run auth:setup
npm.cmd run db:check
npm.cmd run dev -- --port 3002
```

`auth:setup` は `.env.local` の未設定・空欄の `BETTER_AUTH_SECRET` にランダム値を作成し、未設定の `BETTER_AUTH_URL` を `http://localhost:3002` にする。既存の値や他の環境設定を保持し、秘密値を表示しない。

公開環境では `BETTER_AUTH_URL` を実際のHTTPSオリジン、`BETTER_AUTH_SECRET` を環境固有の十分な長さのランダム値に設定する。秘密値・Cookie・接続文字列をGitへ登録しない。localhost以外のHTTPオリジンは拒否する。ローカルでは同ポートのlocalhost / 127.0.0.1を許可し、異なるポートは別の設定が必要。

## DBとHTTPの境界

- `User` を既存の永続userIdとして使用し、`name` / `emailVerified` / `image` を追加。既存の `AuthIdentity` は将来の外部認証subjectとの対応用として維持する。
- `AuthAccount` はローカル資格情報（`password` 列の値はハッシュ）、`AuthSession` は失効可能なセッションと期限、`AuthVerification` はライブラリ互換の確認用領域、`AuthRateLimit` は共有回数制限。合計16テーブル。4テーブル追加のマイグレーションを適用する。
- ライブラリの標準セッショントークンはサーバー専用のDBと署名Cookieで管理する。トークンをAPIのJSONに含めず、ブラウザーのJavaScriptやlocalStorageへ渡さない。`app` スキーマを公開APIの対象にしない。
- 登録は `POST /api/account/signup` のみ。ライブラリの登録ルートへの迂回や未対応のアカウント変更APIは404。公開する認証操作は `POST /api/auth/sign-in/email`、`POST /api/auth/sign-out`、`GET /api/auth/get-session` のみ。後者は真偽値だけを返す。
- 書込時は設定済みOriginを照合し、JSON本文を16KiBまでに制限。メール・パスワード・配列・文字数・同意をサーバーで検証する。登録失敗やログイン失敗の詳細から他人のアカウントの有無を返さない。
- DBで原子的に登録5回/10分/メール、登録全体30回/分、ログイン10回/分/メールを制限する。加えて認証ライブラリがHTTPのIPベース制限を行う。Cookieの共有ドメイン設定や広いtrustedOriginsは追加しない。
- 本番ではHTTPS・制限されたDB実行ロール・プロキシの信頼設定と追加のアクセス制限・期限切れセッションや回数制限行の定期整理・バックアップを運用設計する。ローカル検証だけで公開運用済みと扱わない。

## 検証

Unityの連携は `GET /api/unity/session` が同一オリジンのHttpOnly Cookieをサーバーで照合し、`authenticated` と本人用の最小プロフィール（`userId`・`nickname`・`avatarId`・`teaches`・`wantsToLearn`）だけを返す。メール、非公開の目標・得意、パスワードやセッショントークンは渡さない。応答は `no-store` / `Vary: Cookie`。URLのuserIdは本人指定に使わない。

Unity側はJavaScriptとC#のブリッジで表示を更新する。別タブでの登録・ログイン・ログアウトは、個人情報を含まないBroadcastChannel通知を受けてサーバーへ再照会する。表示中は15秒ごととフォーカス復帰時にも照会する。失効・停止・削除・確認の通信失敗時は前の本人情報を消してゲストへ切り替え、通信失敗は再確認ボタンを表示する。本人の切り替えではUnity内のお試し履歴・予約・投稿も消す。Unityからの識別子を将来の操作APIの認可根拠にはしない。

登録アバターはワールド上部のアイコンと人型アバターの服の色に対応する。動物の3Dモデルへの変換やプロフィール編集は未実装。Unity用の手順と実ビルド検証は [Unity共有手順](../unity/README.md) を参照。

```powershell
npm.cmd run test:db
npm.cmd test
npm.cmd run lint
npm.cmd run build
npx.cmd tsc --noEmit --incremental false
npm.cmd audit --omit=dev
$env:PLAYWRIGHT_EXECUTABLE_PATH = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
npm.cmd run test:smoke
```

ブラウザーテストも明示的な `DATABASE_TEST_URL`（localhostの `*_test` DB）だけを使用し、開発DBと同じ接続先は拒否する。本番ビルドは3012のテストサーバーで起動し、テスト用の認証シークレットを個別生成する。作成した利用者はテスト後に削除する。従来のMac固定の `playwright.config.ts` ではなく、この認証の回帰は `playwright.smoke.config.ts` で実行する。

2026-10-21の最終確認：通常テスト44件・DBテスト19件・本番Webブラウザーテスト39件・Unity実ブラウザー認証連携2件の計104件が成功。ブラウザーテストは正常終了まで確認済み。型検査を含む本番ビルドとLint（エラー0件・既存警告11件）も成功。PC・スマートフォンの画面登録から再ログインに加え、実際のUnityへの本人情報の反映・失効・通信復旧も確認した。検証の詳細は [一連操作の検証記録](./auth-validation-2026-10-21.md)、Unity Webビルドの手順は [Unityの手順書](../unity/README.md) を参照。

## 将来のSupabase移行

2026-10-27にSupabase上の新規DBへ3マイグレーションを適用し、Prisma・Better Authを維持したまま接続した。アプリ専用の制限付きロールで登録・プロフィール保存・ログインを確認し、検証データはロールバックした。ローカルの既存ユーザー・セッションは移していない。接続設定と実施状況は [Supabase接続](./supabase.md) を参照する。

Supabase Authの採用は別判断。将来認証方式を変える場合は、既存のハッシュがそのまま利用できると仮定せず、本人確認を経た資格情報の再設定や `AuthIdentity` との紐づけ、既存セッションの失効を設計・検証する。

参考：[Better AuthのNext.js連携](https://www.better-auth.com/docs/integrations/next)、[Prismaアダプター](https://www.better-auth.com/docs/adapters/prisma)、[Next.js認証ガイド](https://nextjs.org/docs/app/guides/authentication)。
