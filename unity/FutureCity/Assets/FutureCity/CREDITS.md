# 素材のクレジット（Credits）

このフォルダの画像素材は、すべて **CC0（パブリックドメイン）** の実写素材と、そこから作った画像、または自作の画像です。
CC0 のため表記の義務はありませんが、感謝をこめて記載します。

## アニメ調の素材（2026-10-30 版）

| 素材 | 作り方 | ライセンス |
|---|---|---|
| シェーダー（`Resources/FutureCityShaders`） | このプロジェクトで自作（URP 用の HLSL） | このプロジェクトの制作物（CC0 として扱ってかまいません） |
| モデル（`Resources/FutureCityModels`） | Blender の Python スクリプトで自作（プロジェクトの `tools/blender`） | 同上 |
| テクスチャ（`Resources/FutureCityAnime`） | Python で手続き的に生成（プロジェクトの `tools/texgen`） | 同上 |
| 戦闘モードの効果音 | 実行時にプログラムで合成（ファイルなし） | 同上 |

- アニメ調の素材には、外部の素材・アセットストアの商品は使っていません。
- 世界観・建物・人物はミライフのオリジナルです（ほかの作品の素材・キャラクター・形は使っていません）。

## 背景の 360°画像（Assets/FutureCityPhotos）

| ファイル | 元の素材 | 作者・配布元 | ライセンス |
|---|---|---|---|
| `Resources/FutureCityBackdrop/Default_FutureCity_8k.jpg` | 2127年の未来都市（このプロジェクトで制作した 3DCG。Blender 5.0 Cycles で描画） | このプロジェクト | CC0 として扱ってかまいません |
| （上の画像の雲） | 「Parched Canal」HDRI の空から雲だけを取り出して合成 | Poly Haven（旧 HDRI Haven）／ pyvista vtk-data 経由で入手 | CC0 |
| `BackdropAlternatives/ParchedCanal_4k.jpg`（予備の写真） | 「Parched Canal」HDRI（4K）を JPG に | Poly Haven（旧 HDRI Haven） | CC0 |
| `BackdropAlternatives/FlowerRoad_2k.hdr`（予備の写真） | 「Flower Road」HDRI（2K） | Poly Haven（旧 HDRI Haven）／ Google Filament のサンプル同梱品から入手 | CC0 |

- 予備の写真は、地平線より下（広場では湖の水面にかくれて見えない部分）をファイルを小さくするため単色にしています。空と地平線は元のままです。
- 未来都市の画像の空は Blender の物理的な空のモデル（多重散乱）、街は手続き的に生成した約1万7千棟の建物です。

## 広場のテクスチャ（…/Resources/FutureCityTextures。Lawn・Soil・Bark の法線マップは Assets/FutureCityPhotos 側）

| ファイル | 元の素材 | 配布元 | ライセンス |
|---|---|---|---|
| `Bark_*` | bark_brown_02 | Poly Haven（旧 Texture Haven） | CC0 |
| `Wood_*` | brown_planks_04 | Poly Haven（旧 Texture Haven） | CC0 |
| `Soil_*` | forrest_ground_03 | Poly Haven（旧 Texture Haven） | CC0 |
| `Paving_*`（石の凹凸の一部） | rock_05 | Poly Haven（旧 Texture Haven） | CC0 |
| `Metal_*` | Metal009 | ambientCG（旧 CC0 Textures） | CC0 |
| `Fabric_*` | Fabric026 | ambientCG（旧 CC0 Textures） | CC0 |
| `Paving_*`（パステルの石畳）、`WhiteStone_*`、`Lawn_*`、`Hedge_*`、`Water_Normal`、`Foliage_*`（葉・桜）、`Flowers_*`、`Grass_*` | 自作（プログラムで生成） | このプロジェクト | CC0 として扱ってかまいません |

- 実写テクスチャは npm パッケージ「aframe-enviropacks」（Zach Capalbo）に同梱の CC0 素材から取得し、Unity URP 用に「色・法線・マスク（金属・AO・なめらかさ）」の組に変換しました。
- ambientCG の法線マップは DirectX 方式のため、Unity（OpenGL 方式）に合わせて緑チャンネルを反転しています。

## 自分の写真に差しかえるとき

`Tools > Future City > 背景の写真を入れるフォルダを開く` で開くフォルダに、360°写真（横:縦 = 2:1）を入れてください。
おすすめ（どちらも CC0）：

- Poly Haven「Modern Buildings 2」 https://polyhaven.com/a/modern_buildings_2 （川ぞいの広場とガラスのビル、晴れ）
- Poly Haven「Canary Wharf」 https://polyhaven.com/a/canary_wharf （ロンドンの高層ビル街）


## アバター（CC0ではありません）

- **Creative Characters FREE - Animated Pack** — ithappy、バージョン2.4
- https://assetstore.unity.com/packages/3d/characters/humanoids/creative-characters-free-animated-pack-304841
- **Standard Unity Asset Store EULA** に従って利用。既存のCC0素材とは利用条件が異なります。
- 2026-10-20に取得・導入。人物の組み合わせPrefabと着席姿勢は本プロジェクト側で作成。
- 元素材: `Assets/ithappy/Creative_Characters_FREE/`。アセット単体の再配布用ではありません。
