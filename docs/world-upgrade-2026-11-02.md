# 新ワールドの統合確認（2026-11-02）

FutureCity_Unity_1of3.zip / 2of3.zip / 3of3.zip の2026-10-30版景観を統合。
既存のCreative Charactersアバター（プレイヤー・NPC・背景人物）、着替え、Webプロフィール／認証連携、年齢バッジ、従来の移動・カーソル操作を維持した。
同梱の戦闘モードは今回の景観差し替えに含めていない。

## 検証

- 新景観導入前は新ワールド用テストが想定どおり失敗することを確認。
- 統合後のUnity EditModeテスト35件成功（アバター・Webプロフィール・着替え・新シェーダー）。
- Unity 6000.4.2f1 / WebGLビルド成功。
- Chromeで実Webビルド起動、描画、矢印キー移動（約2.63m）、カーソル表示を確認。JavaScript/C#実行時例外なし。
- 実サイト（localhost:3002）の既存Playwrightテスト3件成功（TOP遷移・Unity起動・初期HTML）。
- アバター素材38ファイル、年齢バッジ8ファイル、WebGLテンプレート5ファイルは元と同一。
- 起動時に素材名Accentの警告が1件あるが、直後に部屋ごとの色へ置き換える処理をレビューで確認。表示・操作は成功。
- アバターの版権表記を新素材のCREDITSに保持した。

画面: [新ワールド](world-upgrade-2026-11-02.png)

## ビルド

Unityメニュー Tools > Future City > Recreate Scene and Build Web for melife は、景観シーンを再生成してWebビルドする。
通常の npm run build:unity はシーンを保持してビルドする。
生成済みのWebビルドは public/unity/FutureCity に配置（Gitの管理対象外）。
本番へのデプロイ、commit、pushは実施していない。

ログイン連携コードと既存の認証テストは保持したが、実アカウント／DBを使う新規ログイン検証は今回実施していない。
差し替え前に上書き対象ファイルを /Users/hirotaka/Desktop/melife-world-backup-20261102 に退避した。
