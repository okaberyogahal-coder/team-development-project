"""中央広場のモデル（Blender で作って MMSH に書き出す）

  Fountain        … 2 段の噴水（外の水盤・中央の柱・上の皿・金の柱）。原点＝水盤の中心の床
  SculptureRing0-2… 輪の彫刻（帯状の断面＋内側の金の線）。原点＝輪の中心、輪は XY 面（Unity の XZ 面）
  SculptureOrb    … 光の球
  BenchCurved     … 半径 11.5m の円に沿った曲面のベンチ（白い石の脚＋木の座面と背もたれ）。前＝Blender +Y（Unity +Z）
  LampPost        … 細身の白い街灯（曲がった腕の先に円盤の灯り）

使い方: bvenv/bin/python plaza_models.py <出力フォルダ> [モデル名...]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mtb
import bpy, bmesh
import numpy as np
from mathutils import Vector

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
ONLY = set(sys.argv[2:])


# ------------------------------------------------------------------ 共通
def ground_occluder(size=40.0, z=0.0):
    """AO を焼くときだけ置く地面（書き出さない）"""
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=2, y_segments=2, size=size / 2)
    for v in bm.verts:
        v.co.z = z
    return mtb.obj_from_bmesh("__ground", bm, "Default", smooth=False)


def remap_ao(objs, lo=0.32, gamma=0.75):
    """焼いた AO をやわらかく（真っ黒にしない）：ao' = lo + (1-lo) * ao^gamma"""
    for o in objs:
        ca = o.data.color_attributes.get("ao")
        if ca is None:
            continue
        a = np.zeros(len(ca.data) * 4, np.float32)
        ca.data.foreach_get("color", a)
        a = a.reshape(-1, 4)
        v = np.clip(a[:, 0], 0, 1) ** gamma
        v = lo + (1 - lo) * v
        a[:, 0] = a[:, 1] = a[:, 2] = v
        ca.data.foreach_set("color", a.ravel())


def finish(name, objs, ao=True, ground=True, ao_dist=0.9, samples=32, lo=0.32):
    for o in objs:
        mtb.apply_modifiers(o)
    occ = ground_occluder() if (ao and ground) else None
    if ao:
        mtb.bake_ao(objs, samples=samples, distance=ao_dist)
        remap_ao(objs, lo=lo)
    if occ is not None:
        bpy.data.objects.remove(occ, do_unlink=True)
    mtb.export_mmsh(objs, os.path.join(OUT, name + ".bytes"))


def want(name):
    return not ONLY or name in ONLY


def smooth_angle(o, deg=50):
    mtb.set_smooth_by_angle(o, deg)
    return o


# ------------------------------------------------------------------ 噴水
def fountain():
    mtb.reset()
    objs = []
    # 外の水盤：外壁を上がり、丸い縁を越えて、内壁を下りる（表＝たどる向きの右側）
    basin = [(6.20, 0.00), (6.25, 0.05), (6.27, 0.14), (6.27, 0.33), (6.25, 0.45), (6.20, 0.52), (6.12, 0.565),
             (6.02, 0.58), (5.92, 0.575), (5.84, 0.55), (5.79, 0.50), (5.775, 0.42), (5.775, 0.16), (5.74, 0.11), (5.68, 0.10)]
    objs.append(smooth_angle(mtb.lathe("basin", basin, "Stone", segments=128)))
    # 金の帯（外壁の中ほど）
    band = [(6.268, 0.300), (6.292, 0.312), (6.30, 0.335), (6.292, 0.358), (6.268, 0.370)]
    objs.append(mtb.lathe("band", band, "Gold", segments=128))
    # 水盤の底（水色の石）
    floor = [(5.70, 0.10), (3.0, 0.10), (1.40, 0.10)]
    objs.append(mtb.lathe("floor", floor, "PoolFloor", segments=96))
    # 中央の柱と上の皿：柱を上がり、皿の下側を外へ広がり、縁を越えて、皿の内側へ下りる
    col = [(1.42, 0.10), (1.36, 0.16), (1.18, 0.26), (0.92, 0.40), (0.72, 0.58), (0.60, 0.80), (0.57, 1.02), (0.60, 1.22),
           (0.72, 1.40), (1.00, 1.56), (1.42, 1.68), (1.85, 1.77), (2.16, 1.84), (2.33, 1.90), (2.38, 1.96), (2.36, 2.02),
           (2.30, 2.055), (2.22, 2.06), (2.15, 2.04), (2.12, 1.99), (2.11, 1.93), (1.80, 1.89), (1.0, 1.87), (0.30, 1.865)]
    objs.append(smooth_angle(mtb.lathe("column", col, "Stone", segments=96)))
    # 柱の根元の金の輪
    ring = [(1.43, 0.13), (1.47, 0.155), (1.47, 0.185), (1.43, 0.205)]
    objs.append(mtb.lathe("ring", ring, "Gold", segments=96))
    # 金の柱（輪の彫刻と光の球を支える）
    stem = [(0.30, 1.865), (0.30, 1.90), (0.22, 1.98), (0.12, 2.12), (0.095, 2.40), (0.090, 3.40), (0.15, 3.52), (0.16, 3.58),
            (0.15, 3.64), (0.088, 3.76), (0.080, 4.30), (0.11, 4.38), (0.10, 4.46), (0.0, 4.50)]
    objs.append(smooth_angle(mtb.lathe("stem", stem, "Gold", segments=48), 40))
    # 水面（下の水盤・上の皿）
    w1 = mtb.lathe("water1", [(5.78, 0.44), (1.0, 0.44)], "Water", segments=96)
    w2 = mtb.lathe("water2", [(2.13, 1.985), (0.30, 1.985)], "Water", segments=96)
    # 水は AO を焼かない（別に書き出して最後にまとめる）
    finish("Fountain", objs + [w1, w2], ao=True)


# ------------------------------------------------------------------ 輪の彫刻
def ring_obj(name, R, w=0.20, h=0.40, rr=0.09):
    path = [Vector((math.cos(a) * R, math.sin(a) * R, 0)) for a in np.linspace(0, 2 * math.pi, 160, endpoint=False)]
    prof = mtb.rounded_rect_profile(w, h, rr, n=4)
    o = mtb.sweep(name, path, prof, "Stone", closed_path=True, closed_profile=True, up=(0, 0, 1))
    return o


def sculpture():
    for k, R in enumerate((3.4, 3.0, 2.6)):
        if not want("SculptureRing%d" % k):
            continue
        mtb.reset()
        o = ring_obj("ring", R)
        # 内側の金の線（輪の内側の面に沿って、上下 2 本）
        objs = [o]
        for zz in (-0.105, 0.105):
            path = [Vector((math.cos(a) * (R - 0.103), math.sin(a) * (R - 0.103), zz)) for a in np.linspace(0, 2 * math.pi, 160, endpoint=False)]
            g = mtb.sweep("gold%d" % (zz > 0), path, mtb.circle_profile(0.018, 8), "Gold", closed_path=True, up=(0, 0, 1))
            objs.append(g)
        finish("SculptureRing%d" % k, objs, ao=False)
    if want("SculptureOrb"):
        mtb.reset()
        o = mtb.uv_sphere("orb", 1.15, "OrbGlow", seg=64, rings=32)
        finish("SculptureOrb", [o], ao=False)


# ------------------------------------------------------------------ 曲面のベンチ
def arc_points(Rp, half_angle, z, n=24, R0=11.5):
    """曲率の中心 (0, R0) から半径 Rp の円弧（ベンチの前が +Y）"""
    return [Vector((Rp * math.sin(t), R0 - Rp * math.cos(t), z)) for t in np.linspace(-half_angle, half_angle, n)]


def bench():
    mtb.reset()
    objs = []
    R0 = 11.5
    half = 1.32 / R0
    # 座面の板（5 枚）
    for i, off in enumerate((-0.21, -0.105, 0.0, 0.105, 0.21)):
        pts = arc_points(R0 - off, half, 0.43, n=28)
        o = mtb.sweep("slat%d" % i, pts, mtb.rounded_rect_profile(0.088, 0.04, 0.012, n=2), "Wood", up=(0, 0, 1))
        objs.append(o)
    # 背もたれ（3 枚、少し後ろへ傾ける）
    for i, (off, z) in enumerate(((-0.285, 0.58), (-0.31, 0.70), (-0.335, 0.82))):
        pts = arc_points(R0 - off, half * 0.97, z, n=28)
        o = mtb.sweep("back%d" % i, pts, mtb.rounded_rect_profile(0.04, 0.085, 0.012, n=2), "Wood", up=(0, 0, 1))
        objs.append(o)
    # 白い石の脚（両端と中央）：横から見て「L」をやわらげた形
    for t in (-0.80, 0.0, 0.80):
        ang = t * half
        cx, cy = (R0) * math.sin(ang), R0 - R0 * math.cos(ang)
        side = [(0.0, -0.30), (0.0, 0.26), (0.40, 0.26), (0.40, 0.22), (0.44, -0.25), (0.88, -0.36), (0.88, -0.30)]
        # 側面形を押し出して面取り（厚さ 0.11）
        bm = bmesh.new()
        prof2d = [(-0.27, 0.0), (0.27, 0.0), (0.27, 0.06), (0.24, 0.40), (0.20, 0.42), (-0.20, 0.42), (-0.30, 0.46),
                  (-0.36, 0.90), (-0.41, 0.90), (-0.36, 0.42), (-0.30, 0.06)]
        verts = []
        for (y, z) in prof2d:
            verts.append(bm.verts.new((-0.055, y, z)))
        for (y, z) in prof2d:
            verts.append(bm.verts.new((0.055, y, z)))
        n = len(prof2d)
        bm.faces.new([verts[i] for i in range(n)][::-1])
        bm.faces.new([verts[n + i] for i in range(n)])
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new([verts[i], verts[j], verts[n + j], verts[n + i]])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        o = mtb.obj_from_bmesh("leg", bm, "Stone", smooth=False)
        mtb.add_bevel(o, 0.035, segments=3, angle=30)
        o.rotation_euler = (0, 0, -ang)
        o.location = (cx, cy, 0)
        mtb.apply_modifiers(o)
        mtb.apply_transform(o)
        smooth_angle(o, 35)
        objs.append(o)
    # 脚の上の金の小さな飾り（座面の両端）
    for t in (-1.0, 1.0):
        ang = t * half
        cx, cy = (R0 + 0.0) * math.sin(ang), R0 - R0 * math.cos(ang)
        g = mtb.uv_sphere("cap", 0.035, "Gold", seg=16, rings=8, center=(cx, cy - 0.22, 0.46))
        objs.append(g)
    finish("BenchCurved", objs, ao=True, ao_dist=0.6)


# ------------------------------------------------------------------ 街灯
def lamp():
    mtb.reset()
    objs = []
    base = [(0.21, 0.0), (0.215, 0.03), (0.20, 0.07), (0.15, 0.12), (0.11, 0.20), (0.088, 0.36), (0.076, 0.60),
            (0.066, 2.4), (0.058, 3.8), (0.064, 3.86), (0.064, 3.92), (0.050, 3.98), (0.0, 4.00)]
    objs.append(smooth_angle(mtb.lathe("pole", base, "Stone", segments=32), 40))
    ring = [(0.081, 0.58), (0.094, 0.60), (0.094, 0.64), (0.081, 0.66)]
    objs.append(mtb.lathe("ring", ring, "Gold", segments=32))
    # 曲がった腕（柱の上から前へ）
    ctrl = [Vector((0, 0, 3.78)), Vector((0, 0.02, 4.02)), Vector((0, 0.18, 4.22)), Vector((0, 0.42, 4.26)), Vector((0, 0.64, 4.18))]
    pts = mtb.catmull(ctrl, samples_per_seg=8)
    objs.append(mtb.sweep("arm", pts, mtb.circle_profile(0.032, 10), "Stone", up=(1, 0, 0)))
    # 灯り（上は白い笠、下は光る面、縁に金の輪）
    hx, hy, hz = 0.0, 0.68, 4.12
    shade = [(0.0, 0.10), (0.10, 0.098), (0.22, 0.085), (0.31, 0.05), (0.345, 0.012), (0.34, -0.005), (0.30, -0.012)]
    s = mtb.lathe("shade", shade[::-1], "Stone", segments=48, center=(hx, hy, hz))
    objs.append(smooth_angle(s, 40))
    glow = [(0.0, -0.02), (0.30, -0.012)]      # 外へたどる → 表は下向き
    objs.append(mtb.lathe("glow", glow, "Emissive", segments=48, center=(hx, hy, hz)))
    rim = [(0.338, -0.002), (0.352, 0.006), (0.352, 0.022), (0.336, 0.03)]
    objs.append(mtb.lathe("rim", rim, "Gold", segments=48, center=(hx, hy, hz)))
    finish("LampPost", objs, ao=True, ao_dist=0.5)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    if want("Fountain"):
        fountain()
    sculpture()
    if want("BenchCurved"):
        bench()
    if want("LampPost"):
        lamp()


# ------------------------------------------------------------------ 4 機能の入口のアーチ（色の帯は "Accent"：門ごとに材質を差しかえる）
def arch_curve(half_span, height, n=64, power=0.72):
    pts = []
    for t in np.linspace(0, math.pi, n):
        x = -half_span * math.cos(t)
        z = height * (math.sin(t) ** power)
        pts.append(Vector((x, 0.0, z)))
    return pts


def gate(name="GateArch", half_span=4.6, height=5.8, emblem_r=0.78, accent=True):
    """入口のアーチ：前後 2 本の白いアーチを金の短い梁でつなぎ、てっぺんに紋章、下に灯り"""
    mtb.reset()
    objs = []
    depth = 0.95
    # 前のアーチ（太め・根元が太く上が細い）と後ろのアーチ（細め・少し低い）
    for k, (yy, hs, w, d) in enumerate(((depth * 0.5, 1.0, 0.40, 0.46), (-depth * 0.5, 0.94, 0.28, 0.34))):
        path = [p + Vector((0, yy, 0)) for p in arch_curve(half_span, height * hs)]
        prof = mtb.rounded_rect_profile(w, d, min(w, d) * 0.32, n=3)
        o = mtb.sweep("arch%d" % k, path, prof, "Stone", up=(0, 1, 0),
                      scale_fn=lambda t: 1.12 - 0.3 * math.sin(t * math.pi))
        objs.append(smooth_angle(o, 50))
        if accent and k == 0:
            for sy in (-1, 1):
                band = [p + Vector((0, yy + sy * (d * 0.5 + 0.012), 0)) for p in arch_curve(half_span, height * hs)]
                band = [p for p in band if p.z > 1.0]
                objs.append(mtb.sweep("band%d" % sy, band, mtb.rounded_rect_profile(0.15, 0.014, 0.005, n=1), "Accent", up=(0, 1, 0)))
    # 前後をつなぐ金の梁
    front = arch_curve(half_span, height)
    back = arch_curve(half_span, height * 0.94)
    for t in (0.14, 0.27, 0.40, 0.60, 0.73, 0.86):
        i = int(t * (len(front) - 1))
        a = front[i] + Vector((0, depth * 0.5 - 0.2, 0))
        bb = back[i] + Vector((0, -depth * 0.5 + 0.12, 0))
        objs.append(mtb.sweep("rib", [a, (a + bb) * 0.5, bb], mtb.circle_profile(0.035, 8), "Gold", up=(0, 0, 1)))
    # 内側の金の線（前のアーチ）
    inner = [p + Vector((0, depth * 0.5, 0)) for p in arch_curve(half_span - 0.26, height - 0.26)]
    objs.append(mtb.sweep("gold", inner, mtb.circle_profile(0.032, 8), "Gold", up=(0, 1, 0)))
    # 足もとの台座（前後のアーチをまとめて受ける）
    for sx in (-1, 1):
        b = mtb.rounded_box("plinth", 1.0, depth + 0.7, 0.34, 0.12, "StoneWarm", center=(sx * half_span, 0, 0))
        objs.append(b)
        g = mtb.rounded_box("goldcap", 1.04, depth + 0.74, 0.04, 0.015, "Gold", center=(sx * half_span, 0, 0.34))
        objs.append(g)
    # てっぺんの紋章（前のアーチの頂上、前後に面）
    cz = height + 0.12
    cy = depth * 0.5
    # 表＝たどる向きの右側：下の面を外へ → 縁を上へ → 上の面を内へ
    disc = mtb.lathe("disc", [(0.0, -0.10), (emblem_r, -0.10), (emblem_r + 0.04, -0.06), (emblem_r + 0.04, 0.06), (emblem_r, 0.10), (0.0, 0.10)], "Gold", segments=64)
    disc.rotation_euler = (math.pi / 2, 0, 0)
    disc.location = (0, cy, cz)
    mtb.apply_transform(disc)
    objs.append(disc)
    for sy in (-1, 1):
        # 面：色（またはやわらかく光る面）の円板と白い輪
        # 内へたどる → 表は +Z
        face = mtb.lathe("face", [(emblem_r - 0.1, 0.0), (0.0, 0.0)], "Accent" if accent else "Emissive", segments=64)
        ring = mtb.lathe("fring", [(emblem_r * 0.62, 0.0), (emblem_r * 0.52, 0.0)], "Stone", segments=64)
        for o, off in ((face, 0.104), (ring, 0.108)):
            # lathe の面は +Z 向き。X 軸まわりに -90° で +Y 向き、+90° で -Y 向き
            o.rotation_euler = (-math.pi / 2 if sy > 0 else math.pi / 2, 0, 0)
            o.location = (0, cy + sy * off, cz)
            mtb.apply_transform(o)
            objs.append(o)
    # アーチの下に吊るした灯り
    lz = height - 0.75
    objs.append(mtb.sweep("chain", [Vector((0, cy, height - 0.2)), Vector((0, cy, lz + 0.18))], mtb.circle_profile(0.012, 6), "Gold", up=(1, 0, 0)))
    lamp = mtb.uv_sphere("lantern", 0.16, "Emissive", seg=24, rings=12, center=(0, cy, lz))
    objs.append(lamp)
    cap = mtb.lathe("cap", [(0.16, 0.10), (0.17, 0.14), (0.12, 0.22), (0.0, 0.24)], "Gold", segments=24, center=(0, cy, lz))
    objs.append(cap)
    finish(name, objs, ao=True, ao_dist=0.8)


def kiosk():
    """案内端末：角の丸い白い台＋斜めの画面（色の枠・光る画面）"""
    mtb.reset()
    objs = []
    body = mtb.lathe("body", [(0.36, 0.0), (0.37, 0.04), (0.33, 0.12), (0.24, 0.35), (0.20, 0.75), (0.22, 0.95), (0.26, 1.02), (0.0, 1.05)], "Stone", segments=48)
    objs.append(smooth_angle(body, 45))
    ring = mtb.lathe("ring", [(0.205, 0.70), (0.225, 0.72), (0.225, 0.76), (0.205, 0.78)], "Gold", segments=48)
    objs.append(ring)
    # 画面（前＝+Y に 25° 傾ける）
    frame = mtb.rounded_box("frame", 0.86, 0.07, 0.56, 0.03, "Accent", center=(0, 0, 0))
    scr = mtb.rounded_box("screen", 0.76, 0.02, 0.46, 0.015, "EmissiveCool", center=(0, 0.035, 0.05))
    for o in (frame, scr):
        o.rotation_euler = (math.radians(-25), 0, 0)
        o.location = (0, 0.02, 1.0)
        mtb.apply_modifiers(o)
        mtb.apply_transform(o)
    objs += [frame, scr]
    finish("InfoKiosk", objs, ao=True, ao_dist=0.5)


def skywalk_pillar():
    """空中回廊の柱：根元と頭が広がる白い柱（高さ 6m）"""
    mtb.reset()
    prof = [(0.62, 0.0), (0.62, 0.05), (0.56, 0.12), (0.44, 0.3), (0.36, 0.7), (0.33, 3.0), (0.36, 4.8), (0.48, 5.5), (0.72, 5.85), (0.80, 5.95), (0.80, 6.0), (0.0, 6.0)]
    p = mtb.lathe("pillar", prof, "Stone", segments=40)
    smooth_angle(p, 45)
    g = mtb.lathe("ring", [(0.335, 1.0), (0.36, 1.03), (0.36, 1.09), (0.335, 1.12)], "Gold", segments=40)
    finish("SkywalkPillar", [p, g], ao=True, ao_dist=0.8)


if __name__ == "__main__":
    if want("GateArch"):
        gate("GateArch", 4.6, 5.8, 0.78, True)
    if want("WelcomeArch"):
        gate("WelcomeArch", 5.4, 7.0, 1.05, False)
    if want("InfoKiosk"):
        kiosk()
    if want("SkywalkPillar"):
        skywalk_pillar()


def pot_round():
    """丸い植木鉢（白い石・金の縁）。上の土は低木を植える前提で少し低く"""
    mtb.reset()
    prof = [(0.36, 0.0), (0.40, 0.04), (0.46, 0.22), (0.50, 0.46), (0.53, 0.56), (0.55, 0.62), (0.52, 0.66), (0.47, 0.66), (0.46, 0.60)]
    p = mtb.lathe("pot", prof, "Stone", segments=40)
    smooth_angle(p, 45)
    band = mtb.lathe("band", [(0.503, 0.44), (0.523, 0.455), (0.528, 0.49), (0.51, 0.505)], "Gold", segments=40)
    soil = mtb.lathe("soil", [(0.465, 0.6), (0.0, 0.62)], "Soil", segments=40)
    finish("PotRound", [p, band, soil], ao=True, ao_dist=0.5)


if __name__ == "__main__":
    if want("PotRound"):
        pot_round()
