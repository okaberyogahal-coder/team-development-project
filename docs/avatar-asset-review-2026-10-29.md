# 共有アバターフォルダの確認（2026-10-29）

対象：`origin/feature/unity-avatar-futurecity`、コミット `55061b7`「アバターのフォルダを追加」。比較先：現在のシステム（`9a96936`）。

## 結論

共有されたCreative Characters FREEのモデル・衣装は、現在のシステムが既に使用しているものと同じ。追加のモデル・衣装を取り込む必要はなく、今回の確認でランタイムや保存形式は変更していない。ブランチ全体のマージもしていない。

現在は `ImportedAvatar` と `AvatarWardrobeCatalog` を通じて、プレイヤーとNPCへ同素材を使用する。アバター作成画面は服・顔・髪・小物・コスチュームと色を変更し、既存のブラウザ用appearance形式で保存する。

## 比較した内容

Git blobとZIPの実バイトを比較した。見た目の推測だけで同一と判断していない。

| 対象 | 結果 |
|---|---|
| FBX | 36件すべて既存ファイルと完全一致 |
| Prefab | 36件すべて完全一致 |
| マテリアル | 4件すべて完全一致 |
| テクスチャ | 1件完全一致 |
| 素材提供元のC# | 59件すべて完全一致 |
| AnimatorController | 2件すべて完全一致 |
| ZIP内の非metaファイル | 156件完全一致。既存Git管理対象にない2件はURPからHDRP／Built-inへの変換パッケージ |

素材フォルダで変わっていた159件の内訳はmeta157件、デモシーン1件、Editor asmdef1件。metaのGUID変更はEditor asmdefの1件のみ。asmdefの実質的な参照先・Editor制約は同じ。これらは新しいモデルや着せ替え項目ではない。

## 別サイト用の新規コード

以下は素材の重複とは別に、新規コードとして確認した。

- `MiraifuCreativeAvatar`：8スロットと髪・上着・ズボンのHEX色を適用し、URLの `avatar` JSONも読み取る。
- `MiraifuCreativeFutureCityPlayer`：起動後に既存 `PlayerWalker.body` を新しいPrefabへ差し替え、元のbodyを削除する。
- `MiraifuCreativeSetup`：Prefabと独立した確認用シーンを生成するEditorツール。
- `MiraifuCreativeFutureCityBuild`／`MiraifuCreativeWebPreviewBuild`：別のWebサイト構成向けのWebGL出力ツール。

現在の衣装UIと保存処理は `walker.body` の `ImportedAvatar` を参照するため、上記の差し替え処理を直接適用すると、その参照と衣装設定が使えなくなる。新しい `AvatarChoice` も現在のappearance v2とは互換ではなく、自動的に読み替えて保存しない。

また、受領コードの出力先は `Miraifu_Website` 配下であり、現在のNext.jsの `public/unity/FutureCity` とは異なる。既存のログイン・本人表示ブリッジも置き換えない。

素材を再導入する必要が生じた場合は、既存の `Tools > Future City > Install Creative Characters FREE into world` と `npm run build:unity` の経路を利用する。HEX自由色や受領JSONとの互換が必要になった場合は、既存の保存データを維持する変換設計を別途行う。

## 検証

Unity 6000.4.2f1のEditModeで次の既存テスト群を実行し、18件成功・失敗0件・スキップ0件を確認した。

- `InstalledAvatarTests`：実モデルのHumanoidリグ、描画材質、歩行時の脚、着席姿勢。
- `AvatarIntegrationTests`：モデルと既存の人物・移動処理の接続。
- `WardrobeTests`：衣装カタログと着せ替え。
- `AvatarThemeTests`：衣装設定画面のテーマ・レイアウト。

検証結果XML：`/private/tmp/melife-avatar-review-tests.xml`（一時ファイル、Git対象外）。今回の確認のためにWebGLを再ビルドしたとは扱わない。Editor起動による描画設定ファイルの自動バージョン更新は元に戻した。
