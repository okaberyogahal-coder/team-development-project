# モデル・テクスチャを作り直すためのスクリプト

Unity のプロジェクトには入れません（Unity は Play 時に `Assets/FutureCity/Resources/` の完成品だけを読み込みます）。
形や色を変えたいときに、ここのスクリプトを直して実行し、できたファイルを `Resources` に上書きします。

## 必要なもの（すべて無料）

- Python 3.11
- Blender を Python から使うためのモジュール `bpy`（Blender 5.0 で確認）：`pip install bpy`
- `numpy`、`Pillow`：`pip install numpy pillow`

（Blender 本体のアプリは不要です。`bpy` は Blender と同じ中身を Python のモジュールにしたものです）

## モデル（.bytes ＝ ミライフ独自の MMSH 形式）

| スクリプト | 作るもの |
|---|---|
| `blender/plaza_models.py` | 噴水・輪の彫刻・光る球・曲面のベンチ・街灯・入口のアーチ・ようこそのアーチ・案内端末・空中回廊の柱・植木鉢 |
| `blender/skyline_models.py` | 遠景の未来の塔（TowerA〜F）・円盤の展望塔 |
| `blender/character_models.py` | 人物の部品（胴・縁どり・腰・太もも・すね・靴・袖・手・頭・顔・ひとみ・髪 5 種・めがね） |
| `blender/room_models.py` | 交流室の建物・家具・庭のベンチ・パラソル・藤棚・花壇・カフェのテーブル・船 |

実行例（`tools` フォルダで）：

```
python blender/plaza_models.py out              … 全部
python blender/plaza_models.py out Fountain     … 噴水だけ
python blender/character_models.py out ChrHead,ChrFace
python blender/room_models.py out RoomPavilion
```

できた `out/*.bytes` を `Assets/FutureCity/Resources/FutureCityModels/` に上書きして、Unity で Play。

- 座標：Blender（Z 上・前＝+Y）で作り、書き出すときに Unity（Y 上・前＝+Z）へ変換します。
- 材質名（"Stone" "Gold" "Wood" "Glass" "Accent" "Skin" "Hair" など）が、そのまま Unity 側の材質（`MiraiToonMaterials`）に対応します。
- 頂点色：RGB＝色むら、A＝焼き込んだ環境遮蔽（AO）。UV2 の x＝風の効き。
- 人物の部品は「ぶら下がる関節」が原点です（関節の位置は `PersonFactory.BuildAnime` と同じ）。

## テクスチャ

```
python texgen/anime_textures.py out             … 石畳・芝生・木・花・雲（MT_*.png）
python texgen/leaves.py out/MT_Leaves.png       … 葉と桜のアトラス
```

できた PNG を `Assets/FutureCity/Resources/FutureCityAnime/` に上書き。取り込み設定は `Editor/FutureCityImport.cs` が自動でそろえます。

`preview/mmsh.py` は MMSH 形式の読み書き（モデルのスクリプトが使います）。
