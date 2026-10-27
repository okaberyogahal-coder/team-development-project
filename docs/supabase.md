# Supabase接続と起動手順

アプリのDBをSupabaseのPostgreSQLでホストする。Web画面 → Next.jsのAPI／サーバー → Prisma → Supabaseという構成で、既存のBetter AuthとUnity本人表示連携を維持する。

Supabaseの公開APIキーやSupabase Authは使用しない。DBの接続文字列はサーバー側だけに保存する。

## 接続状況（2026-10-27）

- 新規プロジェクト：`melife-dev`。Session poolerの接続先は東京リージョン（`ap-northeast-1`）。
- 初期・整合性・認証の全3マイグレーションを適用し、`app` スキーマに16テーブルを作成済み。
- `DIRECT_URL` はマイグレーション担当用。アプリの `DATABASE_URL` は `melife_app` ロールで接続する。
- アプリ用ロールにスーパーユーザー・ロール作成・BYPASSRLS・`app` スキーマの所有権は付与していない。必要なスキーマ使用権限とテーブルのSELECT／INSERT／UPDATE／DELETEを付与する。
- CA証明書と `sslmode=verify-full` を使い、クライアント側TLSとサーバー証明書検証を確認済み。
- Prismaでの読み取りと、Better Authでの登録・プロフィール／設定／確認記録の保存・ログインCookie発行を確認済み。検証はトランザクション内で行い、検証データはすべてロールバックした。
- ローカルDBの既存アカウントやセッションのデータ移送は行っていない。ローカルDBとそのデータは保持する。
- クラウドにあるのはDB。Next.jsの公開ホスティングはまだ行っていない。

## Supabase接続での画面確認（2026-10-27）

Chromeで `http://127.0.0.1:3002` を操作し、実際のSupabaseへ保存される状態で次の10項目を確認した。通常の自動テスト用DBの制限は変更していない。

1. 未ログインでマイページへアクセスするとログイン画面へ戻る。
2. PC画面から新規登録し、プロフィール・学びの設定・3件の確認記録・ハッシュ化された資格情報がSupabaseへ保存される。再読み込み後も保持する。
3. ログアウトでDBセッションを削除し、誤ったパスワードは拒否する。再ログイン後も保存内容を表示する。
4. スマートフォン幅390pxでも画面から登録でき、横にはみ出さず、再読み込み後も保存内容を表示する。
5. 2つのブラウザーコンテキストの利用者が分離され、他者IDをURLで指定しても本人表示を切り替えられない。Unity向け応答は表示用の5項目だけを返す。
6. Unity本体が起動し、登録した本人の名前・アバター・教えたいこと・学びたいことをC#の適用結果と実描画で確認する。
7. Unityの確認通信を失敗させると前の本人情報を消し、再確認で同じ本人へ復旧する。
8. Unity上部のログアウトでセッションが消え、別タブの再ログインが起動中のUnityへ反映される。
9. 検証用セッションを期限切れにするとUnityがゲストへ戻る。
10. スマートフォンでもログアウト・再ログイン後にプロフィールを保持する。

全10項目が成功。追加でUnityの「5 プロフィール」をクリックし、パネル内の本人の名前・ねこアバター・料理・園芸の表示を実画像で確認した。検証用アカウントと、そのメールに対応する回数制限レコードは各確認終了時に削除し、残存アカウント0件を確認した。対象はランダムID付きの `cloud-check-...@example.invalid` に限定し、既存利用者を削除していない。画面画像と結果はGit対象外の `test-results/supabase-verification-2026-10-27/` に保存する。

<a id="start"></a>

## チームメンバーの起動

Node.jsを用意し、マイグレーション担当から共有されたアプリ用接続情報を `.env.local` に設定する。

1. `npm.cmd ci` で依存関係を入れる。
2. `.env.local` がない場合だけ `.env.example` をコピーする。既存ファイルは上書きしない。
3. `DATABASE_URL` にアプリ用ロールのSession pooler URIを設定する。接続情報はチャットやGitへ貼らず、チームの秘密情報の共有手段で受け渡す。
4. URIの `sslrootcert` を、このPCでの `certificates/supabase-prod-ca-2021.crt` の絶対パスへ合わせる。必要な記号をURLエンコードする。証明書は公開CA証明書であり、DBパスワードではない。
5. 次で起動する。

```powershell
npm.cmd run auth:setup
npm.cmd run db:check
npm.cmd run dev -- --hostname 127.0.0.1 --port 3002
```

`http://127.0.0.1:3002` を開く。`auth:setup` は既存の認証シークレットを保持する。Unity出力がないPCでは、対応Unity Editorで `npm.cmd run build:unity` を別途実行する。

メンバーの通常起動で `db:deploy` や `db:migrate` は実行しない。共有DBのスキーマ変更は担当者が管理する。

## 接続文字列と証明書

SupabaseのConnect画面でDirect → Session pooler → URIを選ぶ。Session poolerはポート5432で、ユーザー名は `<DBロール>.<プロジェクトID>`。ホスト名はConnect画面からそのままコピーし、リージョン名から組み立てない。

アプリ用と管理用のURIは別のDBロールを使う。両方に `sslmode=verify-full` と、CA証明書のパスを指定する `sslrootcert` を設定する。管理用URIをアプリ用の代わりに配らない。

CAはSupabaseのDatabase Settings → SSL Configurationからダウンロードした `prod-ca-2021.crt`。共有用のコピーを `certificates/supabase-prod-ca-2021.crt` に配置した。SHA-256フィンガープリント：

```text
80:70:25:AD:50:D4:ED:21:9D:2C:9C:7D:29:9C:00:4F:82:4E:B0:0C:F7:F6:5A:FE:F6:07:D0:7B:72:E6:CA:FA
```

## スキーマ変更とテスト

担当者だけがマイグレーション用接続を `DIRECT_URL` に設定し、`npm.cmd run db:status` → `npm.cmd run db:deploy` でレビュー済みの変更を適用する。共有DBでは `db:migrate`、`db push`、`migrate reset` を実行しない。

DBテストと認証ブラウザーテストの担当者は、別途用意したローカルPostgreSQLの独立した `*_test` DBを使う。設定は [スキーマ変更と検証](./database.md#スキーマ変更と検証) を参照する。テストスクリプトの接続先制限を外して共有Supabaseへ向けない。

今回の確認でテーブル・制約・予約トリガーは作成済み。教材・予約の操作API、実AI・通話・複数人同期は引き続き未実装／未接続。

参照：[SupabaseのPrisma接続](https://supabase.com/docs/guides/database/prisma)、[接続方式と証明書](https://supabase.com/docs/guides/database/connecting-to-postgres)、[SSL設定](https://supabase.com/docs/guides/platform/ssl-enforcement)。
