# 素材のクレジット（Credits）

このフォルダの画像素材は、すべて **CC0（パブリックドメイン）** の実写素材と、そこから作った画像、または自作の画像です。
CC0 のため表記の義務はありませんが、感謝をこめて記載します。

## 背景の 360°写真（Assets/FutureCityPhotos）

| ファイル | 元の素材 | 作者・配布元 | ライセンス |
|---|---|---|---|
| `Resources/FutureCityBackdrop/Default_ParchedCanal_4k.hdr` | 「Parched Canal」HDRI（4K） | Poly Haven（旧 HDRI Haven）／ pyvista vtk-data 経由で入手 | CC0 |
| `BackdropAlternatives/FlowerRoad_2k.hdr`（予備の写真） | 「Flower Road」HDRI（2K） | Poly Haven（旧 HDRI Haven）／ Google Filament のサンプル同梱品から入手 | CC0 |

- 地平線より下（広場では湖の水面にかくれて見えない部分）は、ファイルを小さくするため単色にしています。空と地平線は元のままです。

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
