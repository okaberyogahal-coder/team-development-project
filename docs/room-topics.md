# 部屋のお題を試す

ローカルの `/plaza` を再読み込みし、「3 つながる」から「この部屋に入る」を選ぶと、お題が開きます。

- 交流カフェ：最近の小さなうれしさ、季節の楽しみなど5題
- 趣味の部屋：やってみたい趣味、写真、植物、音楽、ものづくりの5題
- 学びの部屋：知りたいこと、スマホ、学び方、教えるコツ、次の一歩の5題
- 経験共有の部屋：初挑戦、暮らしの工夫、失敗からの発見、景色、励みになった言葉の5題

各お題には本文と3つの会話のヒントがあります。「次のお題へ」で順に切り替え、下の一覧から直接選ぶこともできます。一覧の下部はスクロールして表示します。

「閉じる」またはEscで部屋へ戻り、左上の「お題を選ぶ」で再表示します。「ロビーへ戻る」で退出します。部屋ごとの選択はこのワールドを開いている間は保持されます。再読み込み後は最初のお題に戻ります。他の参加者への選択の同期や回答の投稿は含みません。

## データと実装

20題のテストデータは `unity/FutureCity/Assets/FutureCity/Resources/RoomTopics.json`。`roomId` は既存のUnityの部屋ID（`learn` / `story` / `cafe` / `hobby`）と対応します。UnityのResourcesとして読み込むため、データを変更した場合は `npm run build:unity` で再生成します。共有DBへのテストアカウントの登録は行いません。

実装は同じディレクトリ構成の `Runtime/Mirai/RoomTopics.cs` と `MiraiWorld.RoomTopics.cs`。

## 検証結果（2026-11-10）

- Unity 6000.4.2f1：46件成功。データ未同梱時の失敗を確認した後にデータを挿入。
- `npm test`：125件成功、失敗・スキップ0件。
- WebGL生成成功。4つの生成ファイルのハッシュを配信先と比較し、一致を確認。
- `npm run test:room-topics`：Chromeで全20題を選択し、部屋間の選択の独立性、再入室時の保持、パネル表示中の移動停止、閉じた後の移動復帰、退出を確認。
- コードレビューで重大な指摘なし。

Chromeのパスを指定してブラウザ検証する場合：

```sh
PLAYWRIGHT_EXECUTABLE_PATH='/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' npm run test:room-topics
```

WebGLは既存の方針どおりGit除外対象です。ローカルには生成済みファイルを反映し、変更前の生成物は `/private/tmp/melife-before-room-topics-20261110` へ退避しました。
