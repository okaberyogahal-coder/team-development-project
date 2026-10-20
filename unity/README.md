# ミライフ Unity 空間

メンバーから受け取った `FutureCity_Unity_1of2.zip` と
`FutureCity_Unity_2of2.zip` の素材・ソースを、同じリポジトリで共有する。

## 現在の状態

`FutureCity/` は Unity 6000.4.2f1 用の URP プロジェクト。
Unity Hub からこのフォルダを追加して開ける。
TOP の「ワールドに入る」は `/plaza` の Unity 専用画面へ進む。
ブラウザー表示には、以下の手順で Web ビルドを作成する必要がある。
ログイン・通話・複数人同期は未接続。

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
Mac 以外や別のインストール先では、環境変数 `UNITY_EDITOR_PATH` に
Unity 実行ファイルのパスを指定する。

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

ログイン、通話、他の参加者の同期などは、空間表示とは別途接続が必要。
同梱 README のデモ機能を、本番の機能として扱わない。
