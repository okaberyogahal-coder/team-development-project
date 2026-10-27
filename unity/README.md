# ミライフ Unity 空間

メンバーから受け取った `FutureCity_Unity_1of2.zip` と
`FutureCity_Unity_2of2.zip` の素材・ソースを、同じリポジトリで共有する。

## 現在の状態

`FutureCity/` は Unity 6000.4.2f1 用の URP プロジェクト。
Unity Hub からこのフォルダを追加して開ける。
TOP の「ワールドに入る」は `/plaza` の Unity 専用画面へ進む。
ブラウザー表示には、以下の手順で Web ビルドを作成する必要がある。
Webのログイン本人の表示名・アバター・教えたいこと・学びたいことを反映する。
未ログインはゲストで見学できる。実通話・複数人同期は未接続。

プレイヤーとワールド内の人物には、導入済みの **Creative Characters FREE - Animated Pack（ithappy / 2.4）** を使用する。5種類の組み合わせ・待機・歩行・着席の設定を共通で適用する。再生成・カスタマイズは [AVATARS.md](AVATARS.md) を参照。
アバター素材は Unity Asset Store EULA の対象で、既存の CC0 テクスチャとは利用条件が異なる。

```
unity/
  FutureCity/
    Assets/
      FutureCity/        # C#・テクスチャ・素材クレジット
      FutureCityPhotos/  # 360°写真・追加テクスチャ
    README.md            # 制作者の元の説明書
    README_2of2.txt       # 2本目の元の説明書
```

ZIP 同梱の仕様資料・AI向け指示ファイルは取り込んでいない。
このリポジトリの既存仕様資料は `docs/` を参照する。

## Unity で編集する

1. Unity Hub で Unity 6000.4.2f1 と Web Build Support を用意する。
2. Add project from disk で `unity/FutureCity/` を追加して開く。
3. インポート完了後、Tools > Future City > Create Scene を実行する。
4. Play で空間・操作・素材を確認する。
5. Unity を閉じ、変更した `.meta`、シーン、`Packages/`、
   `ProjectSettings/` を Assets と一緒に Git に登録する。

`.meta` は素材やシーンの参照に必要なので必ず共有する。

日本語表示用に Noto Sans CJK JP を同梱している。
配布元: https://github.com/notofonts/noto-cjk
ライセンス: `FutureCity/Assets/FutureCity/Resources/Fonts/OFL.txt`（SIL Open Font License 1.1）。

## Git で共有する

Web と Unity はこの1つのリポジトリで共有できる。
Unity 本体は各メンバーの PC にインストールする。
`Library`・`Temp`・ローカルビルドなどは `.gitignore` で除外している。
`.meta` は除外しない。素材の利用条件は
`FutureCity/Assets/FutureCity/CREDITS.md` を参照する。

通常の GitHub のファイル上限は1ファイル100 MiB。
今回取り込んだファイルはすべて上限未満なので、現在の素材に LFS 設定は追加していない。
今後、大きなモデルや画像を追加する場合は、コミット前に Git LFS の利用を検討する。

この取り込み作業では commit・push はしていない。
GitHub に反映する際は、他の変更も確認してから、例えば次のように登録する。

```sh
git add .gitignore unity docs/README.md app/components/Header.tsx app/components/HeroSection.tsx app/plaza/page.tsx app/plaza/unity.css scripts/build-unity.mjs package.json next.config.mjs tsconfig.json tests/browser/unity-entry.spec.ts
git commit -m "Connect TOP page to FutureCity Unity world"
git push
```

## TOP から Unity ワールドを開く

プロジェクトルートで、Unity のエディターを閉じてから実行する。

```sh
npm run build:unity
npm run dev
```

`http://localhost:3000` の「ワールドに入る」から `/plaza` へ進むと、
Unity のワールドが iframe 内で起動する。初回は読み込みに時間がかかる。
TOP に戻るリンクと、ワールドだけを別タブで開くリンクも用意している。

`build:unity` はシーン未作成の場合だけ生成し、Web ビルドを実行して、
`public/unity/FutureCity/` に出力する。Unity 内の
Tools > Future City > Build Web for melife からも実行できる。
Windows と Mac では、`ProjectVersion.txt` のバージョンに一致する
Unity Hub の標準インストール先を自動で使用する。
別のインストール先では、環境変数 `UNITY_EDITOR_PATH` に
Unity 実行ファイルのパスを指定する。

Web Build Support が未導入の場合は、Unity Hub の対象 Editor の
「モジュールを加える」から追加する。Unity CLI がある環境では次でも追加できる。

```powershell
unity install-modules --editor-version 6000.4.2f1 --module webgl --yes
```

## ブラウザーで確認する

Web ビルド後にサイトを3002番ポートで起動し、別のターミナルで実行する。
このテストは既存のサイトに接続し、旧広場サーバーや認証用テストDBを起動しない。

```powershell
npm.cmd run dev -- --hostname 127.0.0.1 --port 3002
# 別のターミナル。Playwright Chromiumを使用する場合はパス指定不要。
$env:PLAYWRIGHT_EXECUTABLE_PATH = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
npm.cmd run test:unity
```

TOPからの遷移、Unityローダーの起動完了、Canvasの表示、起動時のJavaScript例外を確認する。
スクリーンショットは `test-results/` に保存する。
別ポートの場合は `PLAZA_TEST_URL` を指定する。

2026-10-21にWindowsのUnity 6000.4.2f1でWebビルドが成功し、
`http://127.0.0.1:3002/plaza` で確認した。
`test:unity` は2件成功。ローダーの完了、広場への参加、日本語UIと3D空間の表示を
スクリーンショットでも確認した。これは初回起動時の確認であり、後続で追加した本人表示の連携検証は以下の手順に分ける。実通話・複数人同期は未接続。

素材や生成方法の変更をシーンへ反映したい場合は、先に Unity の
Create Scene を実行する（シーンを作り直すため、手動編集がある場合は注意）。

生成ビルドは Git から除外している。メンバーは clone 後にそれぞれビルドする。
サイトを公開するときは、`npm run build:unity` → `npm run build` の順に実行し、
`public/unity/FutureCity/` をサイトと一緒に配信する。
Next.js のみのビルド環境で Unity を実行できない場合は、Unity を使える環境で
生成した同フォルダを、公開前にビルド環境へ渡す。

Web ビルドは非圧縮で出力するため、圧縮用の Content-Encoding 設定は不要。
本体が大きいため、配信の最適化は公開先の決定後に行う。
Unity のキャッシュやビルドではなく、ソースと `.meta` を Git で共有する。

実通話、他の参加者の同期、教材・予約などの保存は別途接続が必要。
同梱 README のデモ機能を、本番の機能として扱わない。

## Webアカウントとの連携

同じサイトの `/api/unity/session` がCookieから本人を確認し、表示用プロフィールだけを返す。
WebGLテンプレートの `session.js` と `WebSession.jslib` が `WebSessionBridge.cs` に渡し、
`MiraiWorld.cs` が登録プロフィールと服の色を適用する。メール・目標・資格情報・トークンはUnityへ渡さない。
Webの登録アバター6種は上部のアイコンと人型の服の色へ対応し、動物型3Dモデルには変更しない。
Unityのプロフィール画面は登録情報の閲覧用。Editor内での従来のオフラインデモは維持する。

ログインはワールドの上部から行い、マイページから「ワールドに入る」で戻れる。
上部のログアウトボタンはUnityを閉じずにゲストへ戻す。
別タブでの認証操作も反映し、表示中の15秒ごとの確認とフォーカス復帰時の確認で失効を検出する。
通信失敗はゲスト表示に切り替え、再確認ボタンを表示する。
本人の切り替え時は前のプロフィールとUnity内のお試しデータを消す。
教材・予約・AI・通話の操作は引き続き保存されないデモである。

生成したWebビルドと本番Webビルドを用意し、独立したローカルテストDBで実際のUnityとの連携を検証する。
開発用のDBや既存サーバーは利用しない。
事前にローカルPostgreSQLの専用 `*_test` DBを `DATABASE_TEST_URL` に設定する。設定は [DBテスト手順](../docs/database.md#スキーマ変更と検証) を参照。

```powershell
npm.cmd run build:unity
npm.cmd run test:db
npm.cmd run build
$env:PLAYWRIGHT_EXECUTABLE_PATH = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
npm.cmd run test:unity:auth
```

2026-10-21に連携追加後のUnity Webビルド成功、`test:unity:auth` は2件成功・終了コード0。
C#適用結果と別タブのログイン・別人切替・ログアウト・通信失敗と復旧・定期照会による期限切れ・
単独ページを確認した。実際の広場内の本人プロフィールと服の色、ログアウト後のゲスト表示も画像で確認した。
WebとDBを含む検証記録は [認証の一連操作の検証](../docs/auth-validation-2026-10-21.md) を参照。
