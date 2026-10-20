# Creative Characters アバター

使用アセット: [Creative Characters FREE - Animated Pack / ithappy](https://assetstore.unity.com/packages/3d/characters/humanoids/creative-characters-free-animated-pack-304841)

Unity の Asset Store で使用するアカウントに追加し、Unity の Package Manager > My Assets からダウンロード・インポートする。Unity 6000 用・URP 用のパッケージが別途同梱されている場合は、このプロジェクトに合うものを使用する。アセットのモデルやテクスチャを別の公開サイトから代用ダウンロードしない。

## ワールドへの接続

導入済みの FREE 2.4 から再生成する場合は `Tools > Future City > Install Creative Characters FREE into world` を実行する。身体・表情・服・髪・靴などを組み合わせた5種類を生成し、全人物に共通で適用する。`Generated/AvatarSources` は組み立て元、`Resources/FutureCityAvatars` はゲームで使う生成物。

待機は `Idle_Breathing`、歩行は `Walk_Forward` を使用する。無料版には着席クリップが含まれていないため、同じ骨格から太ももが前向き・すねが下向きになる姿勢を求め、待機クリップへ脚のヒューマノイドカーブを追加した着席クリップを生成する。

独自に作った完成済みキャラクターを使う場合は `Tools > Future City > Configure Creative Characters Avatars` を開き、Prefab と待機・歩行・着席クリップを指定する。アセット付属のデモシーンや移動コントローラーは、このワールドへ追加しない。

生成先は `Assets/FutureCity/Resources/FutureCityAvatars/Characters/`。プレイヤー・デモ人物・背景の歩行人物・着席人物はすべて `PersonFactory` を通じてこの共通モデルを使用する。元の移動経路、速度、カメラ、クリック操作はそのまま使用する。

アバターの選択ボタンは導入済みモデルの切り替えになる。プロフィールの「アバターの着替え」でトップス・ボトムス・靴・髪型・帽子・眼鏡を個別に変更でき、各部位を8色から選べる。帽子と眼鏡は「なし」も選べる。回転可能なプレビューとワールド内の本人へ即時反映し、選択をこの端末の PlayerPrefs に保存する。別端末への同期は行わない。肌・顔のマテリアルは着色しない。

NPCは名前をもとに服装と色を決定し、再表示しても同じ人物の服装を保つ。服・髪・小物は別々に選ぶため、5つの完成済みモデルだけの繰り返しにはならない。`Wardrobe.asset` と `ClothingColors` は上記インストールメニューで再生成できる。

## 確認

- Unity の Play でプレイヤーと周囲のすべての人物を確認する。
- 停止時・歩行時・着席時の姿勢、地面への接地、向き、人物サイズを確認する。
- アバター選択時に外見が切り替わり、古いモデルが残らないことを確認する。
- プロフィールで各部位の形・色、帽子と眼鏡の着脱、プレビューの回転を確認し、再読み込み後も選んだ外見が復元されることを確認する。
- `npm run build:unity` で Web ビルドを更新し、`/plaza` から確認する。
- Unity Test Runner の EditMode で `FutureCity.Tests` を実行する。

素材本体・生成 Prefab・AnimatorController・必要な `.meta` をチームの共有方針とアセットのライセンスに従って管理する。素材単体の再配布や、本リポジトリの既存 CC0 素材と同じ扱いにしない。導入元と取得バージョンをクレジットに記録する。

未導入の環境では既存の標準人物を表示する。指定アセットの導入完了とは区別する。

## 導入内容

- アセット: Creative Characters FREE - Animated Pack、ithappy、バージョン 2.4
- 取得日: 2026-10-20（ログイン済みアカウントで My Assets へ追加）
- 元素材: `Assets/ithappy/Creative_Characters_FREE/`
- レンダーパイプライン: アセット標準の URP マテリアルを使用
- パッケージに含まれる `Packages/manifest.json` は既存依存関係を上書きしないよう除外してインポート
- 付属エディターコードは追加の Editor 専用 asmdef で分離し、Web ビルドへ含めない
- アバター未導入環境用の従来モデルは互換性のため残すが、導入済み環境では全人物が指定アセットを使用

## 検証結果（2026-10-20）

- Unity EditMode 統合テスト: 9件成功（実アセットのHumanoid、脚の歩行、着席、モデル切り替え、当たり判定、NPC服装の分散、肌と骨格の保持、顔と服装の保存・復元、不正な保存データの補正）
- 既存Webテスト: 44件成功
- Unity Webビルド: 成功
- Chrome: ワールド内のプレイヤーとNPCのモデル・マテリアル表示とNPCの自動歩行を目視確認
- Chrome: プロフィールでトップスとボトムスの形・色、帽子と眼鏡の装着、プレビュー回転を確認。再読み込み後も選んだ赤いトップス・緑のボトムス・帽子・眼鏡がワールドとプレビューへ復元されることを確認
- ブラウザ自動キー入力では `WrongDocumentError: The root document of this element is not valid for pointer lock` が発生し、手動移動の確認は未完了。アバターの骨の歩行アニメーションはUnity側のテストで確認済み
