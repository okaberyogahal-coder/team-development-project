"""遠景の未来都市の建物（Blender で作って MMSH に書き出す）

湖の向こう岸（約 1〜2km 先）に並べるので、細部より「シルエットと帯の模様」をはっきりさせる。
材質：FarBuilding（白）・FarGlass（青いガラス）・Hedge（屋上・テラスの緑）・Gold（飾り）・Emissive（灯り）
AO は高さで付ける（足もとほどわずかに暗い）。原点＝建物の足もとの中心。

使い方: bvenv/bin/python skyline_models.py <出力フォルダ> [モデル名...]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mtb
import bpy, bmesh
import numpy as np
from mathutils import Vector

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
ONLY = set(sys.argv[2:])


def want(n):
    return not ONLY or n in ONLY


def rrect(w, d, r, n=4):
    """角丸の長方形（XY 平面、反時計回り）"""
    pts = []
    r = min(r, w * 0.49, d * 0.49)
    cx, cy = w / 2 - r, d / 2 - r
    for (sx, sy, a0) in ((1, 1, 0), (-1, 1, 90), (-1, -1, 180), (1, -1, 270)):
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n)
            pts.append((sx * cx + math.cos(a) * r, sy * cy + math.sin(a) * r))
    return pts


def ellipse(w, d, n=32):
    return [(math.cos(2 * math.pi * i / n) * w / 2, math.sin(2 * math.pi * i / n) * d / 2) for i in range(n)]


def prism(name, outline, z0, z1, mat, scale_top=1.0, cap_top=True, cap_bottom=False):
    """外形 outline（反時計回り）を z0→z1 に押し出す（側面は外向き、なめらかな陰影は角度で切る）"""
    bm = bmesh.new()
    bot = [bm.verts.new((x, y, z0)) for (x, y) in outline]
    top = [bm.verts.new((x * scale_top, y * scale_top, z1)) for (x, y) in outline]
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([bot[i], bot[j], top[j], top[i]])
    if cap_top:
        bm.faces.new(top)
    if cap_bottom:
        bm.faces.new(bot[::-1])
    # outline は反時計回りなので、側面 [下i, 下j, 上j, 上i] は外向き・上面は上向き（向きの自動修正はしない：開いた筒で裏返るため）
    o = mtb.obj_from_bmesh(name, bm, mat, smooth=True)
    mtb.set_smooth_by_angle(o, 40)
    return o


def height_ao(objs, z_full=35.0, lo=0.72):
    """高さで AO（足もとが少し暗い）"""
    for o in objs:
        me = o.data
        ca = me.color_attributes.get("ao")
        if ca is None:
            ca = me.color_attributes.new(name="ao", type='FLOAT_COLOR', domain='POINT')
        co = mtb.vertex_positions(o)
        v = lo + (1 - lo) * np.clip(co[:, 2] / z_full, 0, 1)
        cols = np.stack([v, v, v, np.ones_like(v)], -1).astype(np.float32)
        if ca.domain == 'POINT':
            ca.data.foreach_set("color", cols.ravel())


def export(name, objs):
    for o in objs:
        mtb.apply_modifiers(o)
    height_ao(objs)
    mtb.export_mmsh(objs, os.path.join(OUT, name + ".bytes"))


# ------------------------------------------------------------------ A：白い帯と青いガラスの角丸タワー
def tower_a():
    mtb.reset()
    objs = []
    W, D, R, Hh = 26, 20, 6, 170
    floor_h = 8.5
    z = 0.0
    k = 0
    while z < Hh - 1:
        g = prism("glass%d" % k, rrect(W - 1.4, D - 1.4, R - 0.7), z, z + floor_h - 2.0, "FarGlass", cap_top=False)
        s = prism("slab%d" % k, rrect(W, D, R), z + floor_h - 2.0, z + floor_h, "FarBuilding")
        objs += [g, s]
        z += floor_h
        k += 1
    # 頂部：ひとまわり小さい箱と輪
    objs.append(prism("crown", rrect(W * 0.7, D * 0.7, R * 0.7), z, z + 14, "FarBuilding", scale_top=0.92))
    objs.append(prism("crownglass", rrect(W * 0.72, D * 0.72, R * 0.72), z + 4, z + 9, "FarGlass", cap_top=False))
    objs.append(mtb.lathe("spire", [(1.6, z + 14), (1.0, z + 26), (0.3, z + 40), (0.0, z + 41)], "Gold", segments=12))
    export("TowerA", objs)


# ------------------------------------------------------------------ B：丸い塔（バルコニーの輪と緑）
def tower_b():
    mtb.reset()
    objs = []
    Hh, r = 210, 13.0
    objs.append(mtb.lathe("core", [(r, 0.0), (r, Hh), (0.0, Hh)], "FarGlass", segments=40))
    z = 10.0
    k = 0
    while z < Hh - 4:
        ring = mtb.lathe("ring%d" % k, [(r - 0.5, z - 1.2), (r + 2.3, z - 1.2), (r + 2.6, z - 0.6), (r + 2.6, z), (r - 0.5, z)], "FarBuilding", segments=40)
        objs.append(ring)
        if k % 3 == 1:
            objs.append(mtb.lathe("green%d" % k, [(r + 2.2, z), (r + 2.2, z + 0.9), (r + 0.6, z + 1.1), (r - 0.2, z + 0.9)], "Hedge", segments=40))
        z += 10.0
        k += 1
    objs.append(mtb.lathe("dome", [(r + 1.0, Hh), (r + 1.0, Hh + 2.0), (r * 0.8, Hh + 8), (r * 0.4, Hh + 12), (0.0, Hh + 13)], "FarBuilding", segments=40))
    objs.append(mtb.lathe("spire", [(1.2, Hh + 12.5), (0.6, Hh + 30), (0.0, Hh + 31)], "Gold", segments=10))
    export("TowerB", objs)


# ------------------------------------------------------------------ C：細く高い楕円の塔（白い縦のひれ）
def tower_c():
    mtb.reset()
    objs = []
    Hh = 260
    objs.append(prism("body", ellipse(15, 11, 32), 0, Hh, "FarGlass", scale_top=0.55))
    for i in range(8):
        a = 2 * math.pi * i / 8
        fin = [(math.cos(a) * 7.6, math.sin(a) * 5.6)]
        # ひれ：細い箱を外側へ
        o = mtb.rounded_box("fin%d" % i, 1.2, 3.2, Hh, 0.4, "FarBuilding")
        o.rotation_euler = (0, 0, a)
        o.location = (math.cos(a) * 7.4, math.sin(a) * 5.3, 0)
        mtb.apply_modifiers(o)
        mtb.apply_transform(o)
        # 上へ行くほど本体に合わせて内側へ（頂点を高さで縮める）
        co = mtb.vertex_positions(o)
        t = np.clip(co[:, 2] / Hh, 0, 1)
        s = 1.0 - 0.45 * t
        co[:, 0] *= s
        co[:, 1] *= s
        o.data.vertices.foreach_set("co", co.ravel())
        o.data.update()
        objs.append(o)
    objs.append(mtb.lathe("top", [(4.6, Hh), (4.0, Hh + 6), (0.8, Hh + 40), (0.0, Hh + 42)], "FarBuilding", segments=24))
    export("TowerC", objs)


# ------------------------------------------------------------------ D：ふたごの塔と空中の渡り廊下
def tower_d():
    mtb.reset()
    objs = []
    for sx, Hh in ((-1, 150), (1, 126)):
        cx = sx * 17
        floor_h = 9.0
        z = 0.0
        k = 0
        while z < Hh - 1:
            g = prism("g%d_%d" % (sx, k), [(x + cx, y) for (x, y) in rrect(14.8, 14.8, 3.6)], z, z + floor_h - 2.2, "FarGlass", cap_top=False)
            s = prism("s%d_%d" % (sx, k), [(x + cx, y) for (x, y) in rrect(16, 16, 4)], z + floor_h - 2.2, z + floor_h, "FarBuilding")
            objs += [g, s]
            z += floor_h
            k += 1
        objs.append(prism("cap%d" % sx, [(x + cx, y) for (x, y) in rrect(12, 12, 3)], z, z + 6, "FarBuilding", scale_top=0.8))
    # 渡り廊下（白い筒＋ガラス）
    bridge = mtb.sweep("bridge", [Vector((-10, 0, 96)), Vector((10, 0, 96))], mtb.rounded_rect_profile(6, 5, 2, n=3), "FarBuilding", up=(0, 0, 1))
    bglass = mtb.sweep("bglass", [Vector((-10, 0, 96.2)), Vector((10, 0, 96.2))], mtb.rounded_rect_profile(6.2, 2.2, 1, n=2), "FarGlass", up=(0, 0, 1))
    objs += [bridge, bglass]
    export("TowerD", objs)


# ------------------------------------------------------------------ E：段々のテラス（屋上の緑）
def tower_e():
    mtb.reset()
    objs = []
    z = 0.0
    for i, (w, d, h) in enumerate(((64, 44, 15), (52, 36, 15), (40, 28, 15), (28, 20, 15))):
        objs.append(prism("glass%d" % i, rrect(w - 1.2, d - 1.2, 8), z, z + h - 3, "FarGlass", cap_top=False))
        objs.append(prism("slab%d" % i, rrect(w, d, 8.5), z + h - 3, z + h, "FarBuilding"))
        objs.append(prism("green%d" % i, rrect(w - 3.0, d - 3.0, 7), z + h, z + h + 1.2, "Hedge"))
        z += h
    objs.append(mtb.lathe("dome", [(8, z + 1.2), (7.6, z + 4), (5, z + 8), (0, z + 9.5)], "FarGlass", segments=32))
    export("TowerE", objs)


# ------------------------------------------------------------------ F：帆のような三日月形の塔
def tower_f():
    mtb.reset()
    objs = []
    Hh = 190
    # 三日月：外側の弧と内側の弧
    outer = [(math.cos(a) * 22, math.sin(a) * 22 - 6) for a in np.linspace(math.radians(25), math.radians(155), 24)]
    inner = [(math.cos(a) * 15, math.sin(a) * 13 - 2) for a in np.linspace(math.radians(155), math.radians(25), 24)]
    outline = outer + inner
    objs.append(prism("sail", outline, 0, Hh, "FarBuilding", scale_top=0.75))
    # ガラスの面（内側の弧の少し手前）
    glass_line = [(math.cos(a) * 14.4, math.sin(a) * 12.4 - 2) for a in np.linspace(math.radians(150), math.radians(30), 24)]
    gl = []
    for zz in range(0, Hh - 10, 12):
        pts = glass_line
        bm = bmesh.new()
        vb = [bm.verts.new((x, y, zz + 2)) for (x, y) in pts]
        vt = [bm.verts.new((x * (1 - 0.25 * (zz + 10) / Hh), y * (1 - 0.25 * (zz + 10) / Hh), zz + 10)) for (x, y) in pts]
        for i in range(len(pts) - 1):
            bm.faces.new([vb[i], vb[i + 1], vt[i + 1], vt[i]])
        o = mtb.obj_from_bmesh("gl%d" % zz, bm, "FarGlass", smooth=True)
        gl.append(o)
    objs += gl
    objs.append(mtb.lathe("needle", [(1.0, 0.0), (0.4, 30), (0.0, 31)], "Gold", segments=8, center=(0, 14, Hh * 1.0 - 4)))
    export("TowerF", objs)


# ------------------------------------------------------------------ 皿のような展望台のある塔（遠景のランドマーク）
def saucer_tower():
    mtb.reset()
    objs = []
    shaft = [(15.0, 0.0), (14.0, 3.0), (9.0, 10.0), (6.0, 22.0), (4.6, 60.0), (3.6, 180.0), (3.2, 232.0)]
    # 皿の下面→縁→上面→上の丸屋根
    saucer = shaft + [(9.0, 234.0), (20.0, 237.0), (27.0, 240.0), (28.0, 242.0), (27.5, 244.0), (20.0, 247.5), (11.0, 250.5),
                      (8.0, 254.0), (5.0, 262.0), (3.0, 268.0), (8.5, 270.0), (9.0, 272.0), (6.0, 275.0), (2.0, 278.0), (1.2, 290.0),
                      (0.6, 320.0), (0.0, 332.0)]
    objs.append(mtb.lathe("tower", saucer, "FarBuilding", segments=48))
    objs.append(mtb.lathe("windows", [(27.6, 240.6), (28.15, 241.6), (28.15, 242.6), (27.7, 243.6)], "FarGlass", segments=48))
    objs.append(mtb.lathe("ring1", [(4.0, 100.0), (5.4, 101.0), (5.4, 103.0), (4.0, 104.0)], "Gold", segments=32))
    objs.append(mtb.lathe("ring2", [(9.1, 270.6), (9.4, 271.0), (9.4, 271.6), (9.1, 272.0)], "Gold", segments=32))
    objs.append(mtb.uv_sphere("beacon", 1.4, "Emissive", seg=12, rings=8, center=(0, 0, 333)))
    export("SaucerTower", objs)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, fn in (("TowerA", tower_a), ("TowerB", tower_b), ("TowerC", tower_c), ("TowerD", tower_d),
                     ("TowerE", tower_e), ("TowerF", tower_f), ("SaucerTower", saucer_tower)):
        if want(name):
            fn()
