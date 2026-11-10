"""アニメ調の人物の部品（PersonFactory の関節の位置・部品名に合わせる）

座標：Blender（Z 上、人物の前＝+Y）で作り、書き出すと Unity（Y 上、前＝+Z）になる。
各部品は「その部品がぶら下がる関節」を原点に作る（Unity では関節の子に置くだけで、手足の振り・座る動きがそのまま使える）。
  ChrTorso, ChrTrim, ChrHip      … 人物の足もと（ルート）が原点
  ChrLeg（太もも）               … 股関節（ルートから (±0.09, 0, 0.84)）が原点
  ChrSleeve, ChrHand             … 肩（(±0.205, 0, 1.30)）が原点
  ChrHead, ChrEyes, ChrFace, ChrHair*, ChrGlasses … 首（(0, 0, 1.405)）が原点
  ChrShin（すね）, ChrShoe       … ひざ（股関節の 0.40m 下）が原点
材質名（＝ Unity 側の材質）：
  Skin  肌（輪郭線あり、影は暖色）         Cloth 服（輪郭線あり）
  Hair  髪（輪郭線あり、つやの輪）          Face  目・まゆ・口（輪郭線なし、頂点色で色分け）
  Iris  ひとみ（Face と同じ材質。人ごとに色を変えるので別の部品にする）
色は人ごとに Unity 側で付ける（MaterialPropertyBlock）。頂点色は「明るさのむら」（ほお紅・髪のつや・靴底など）。

使い方: bvenv/bin/python character_models.py <出力フォルダ> [部品名,部品名...]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mtb
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
WANT = set(sys.argv[2].split(",")) if len(sys.argv) > 2 else None


def want(n):
    return WANT is None or n in WANT


def smoothstep(e0, e1, x):
    t = min(1.0, max(0.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


# ------------------------------------------------------------------ 形を作る道具
class Geo:
    """頂点・面・頂点色をためてオブジェクトにする"""

    def __init__(self):
        self.v, self.f, self.c = [], [], []

    def add_vert(self, p, col=(1, 1, 1)):
        self.v.append(tuple(p))
        self.c.append(tuple(col)[:3] + (1.0,))
        return len(self.v) - 1

    def obj(self, name, mat, smooth=True):
        o = mtb.obj_from_arrays(name, self.v, self.f, mat, smooth)
        mtb.set_point_colors(o, "tint", self.c)
        return o

    def bvh(self):
        return BVHTree.FromPolygons([Vector(p) for p in self.v], self.f)


def sell(w, d, n, p, cx=0.0, cy=0.0, z=0.0):
    """超楕円の輪（上から見て反時計回り）。幅 w（x）・奥行き d（y）"""
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        c, s = math.cos(a), math.sin(a)
        x = math.copysign(abs(c) ** (2.0 / p), c) * w * 0.5
        y = math.copysign(abs(s) ** (2.0 / p), s) * d * 0.5
        pts.append(Vector((cx + x, cy + y, z)))
    return pts


def loft(g, rings, cap_bottom=True, cap_top=True, colors=None):
    """輪（下→上、各輪は上から見て反時計回り）をつないだ筒。面は外向き"""
    n = len(rings[0])
    base = len(g.v)
    for k, r in enumerate(rings):
        col = colors[k] if colors else (1, 1, 1)
        for p in r:
            g.add_vert(p, col)
    for k in range(len(rings) - 1):
        for i in range(n):
            j = (i + 1) % n
            g.f.append((base + k * n + i, base + k * n + j, base + (k + 1) * n + j, base + (k + 1) * n + i))
    if cap_bottom:
        c = sum(rings[0], Vector()) / n
        ci = g.add_vert(c, colors[0] if colors else (1, 1, 1))
        for i in range(n):
            g.f.append((ci, base + (i + 1) % n, base + i))
    if cap_top:
        c = sum(rings[-1], Vector()) / n
        ci = g.add_vert(c, colors[-1] if colors else (1, 1, 1))
        top = base + (len(rings) - 1) * n
        for i in range(n):
            g.f.append((ci, top + i, top + (i + 1) % n))


def nearest_on_path(P, p):
    best, bd = P[0], 1e9
    for i in range(len(P) - 1):
        a, b = P[i], P[i + 1]
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12)))
        q = a + ab * t
        d = (p - q).length_squared
        if d < bd:
            bd, best = d, q
    return best


def face_normal(pts):
    nrm = Vector()
    for k in range(len(pts)):
        a, b = pts[k], pts[(k + 1) % len(pts)]
        nrm += Vector(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
    return nrm


def orient_outward(g, first_face, center_fn):
    """first_face 以降の面を、center_fn(面の中心) から外向きにそろえる"""
    for fi in range(first_face, len(g.f)):
        f = g.f[fi]
        pts = [Vector(g.v[i]) for i in f]
        cen = sum(pts, Vector()) / len(pts)
        nrm = face_normal(pts)
        if nrm.length < 1e-14:
            continue
        if nrm.dot(cen - center_fn(cen)) < 0:
            g.f[fi] = tuple(reversed(f))


def orient_dir(g, first_face, dir_fn):
    """first_face 以降の面を、dir_fn(面の中心) の向きにそろえる"""
    for fi in range(first_face, len(g.f)):
        f = g.f[fi]
        pts = [Vector(g.v[i]) for i in f]
        cen = sum(pts, Vector()) / len(pts)
        nrm = face_normal(pts)
        if nrm.length < 1e-14:
            continue
        if nrm.dot(dir_fn(cen)) < 0:
            g.f[fi] = tuple(reversed(f))


def tube_along(g, path, radii, n=10, col_fn=None, cap0=True, cap1=True):
    """中心線 path に沿った丸い管（radii は各点の半径）。面は外向き"""
    P = [Vector(p) for p in path]
    N = len(P)
    base = len(g.v)
    f0 = len(g.f)
    prev_side = None
    for i in range(N):
        t = (P[min(i + 1, N - 1)] - P[max(i - 1, 0)]).normalized()
        if prev_side is None:
            ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
            side = t.cross(ref).normalized()
        else:
            side = (prev_side - t * prev_side.dot(t)).normalized()
        up = side.cross(t).normalized()
        prev_side = side
        for k in range(n):
            a = 2 * math.pi * k / n
            q = P[i] + (side * math.cos(a) + up * math.sin(a)) * radii[i]
            g.add_vert(q, col_fn(i / (N - 1), q) if col_fn else (1, 1, 1))
    for i in range(N - 1):
        for k in range(n):
            k2 = (k + 1) % n
            g.f.append((base + i * n + k, base + i * n + k2, base + (i + 1) * n + k2, base + (i + 1) * n + k))
    if cap0:
        ci = g.add_vert(P[0], g.c[base][:3])
        for k in range(n):
            g.f.append((ci, base + (k + 1) % n, base + k))
    if cap1:
        ci = g.add_vert(P[-1], g.c[base + (N - 1) * n][:3])
        for k in range(n):
            g.f.append((ci, base + (N - 1) * n + k, base + (N - 1) * n + (k + 1) % n))
    orient_outward(g, f0, lambda c: nearest_on_path(P, c))


def export(name, objs):
    if not want(name):
        return
    mtb.export_mmsh([o for o in objs if o is not None], os.path.join(OUT, name + ".bytes"))


# ------------------------------------------------------------------ 胴（上着）
# (高さ, 幅, 奥行き, 前後のずれ, 角の丸み) — 腰で細く、胸と肩で広い。裾は少し広がる
TORSO_ROWS = [
    (0.790, 0.322, 0.218, 0.000, 2.4), (0.815, 0.326, 0.220, 0.000, 2.5), (0.870, 0.304, 0.202, 0.000, 2.6),
    (0.950, 0.280, 0.190, 0.004, 2.7), (1.030, 0.288, 0.198, 0.008, 2.8), (1.110, 0.312, 0.214, 0.012, 2.8),
    (1.190, 0.330, 0.220, 0.010, 2.8), (1.250, 0.346, 0.212, 0.004, 2.7), (1.292, 0.356, 0.198, -0.004, 2.5),
    (1.322, 0.322, 0.176, -0.008, 2.3), (1.350, 0.258, 0.148, -0.008, 2.2), (1.372, 0.180, 0.118, -0.004, 2.1),
    (1.388, 0.098, 0.090, 0.000, 2.0)]


def torso_geo():
    g = Geo()
    rings = [sell(w, d, 40, p, 0.0, y, z) for (z, w, d, y, p) in TORSO_ROWS]
    cols = [(0.86, 0.86, 0.86) if k == 0 else (1, 1, 1) for k in range(len(rings))]
    loft(g, rings, cap_bottom=True, cap_top=True, colors=cols)
    return g


def torso():
    mtb.reset()
    g = torso_geo()
    export("ChrTorso", [g.obj("torso", "Cloth")])


def trim():
    """上着の縁どり：立ち襟・前立て・肩から胸への切りかえ線・裾の帯（人ごとに色を変える）"""
    mtb.reset()
    tg = torso_geo()
    bvh = tg.bvh()
    g = Geo()

    def surf_y(x, z, off):
        """前（+Y）から胴の表面へ"""
        hit = bvh.ray_cast(Vector((x, 1.0, z)), Vector((0, -1, 0)))
        if hit[0] is None:
            return None, None
        return hit[0] + hit[1] * off, hit[1]

    def surf_radial(phi, z, off):
        d = Vector((math.sin(phi), math.cos(phi), 0.0))
        hit = bvh.ray_cast(Vector((0, 0, z)) + d * 1.0, -d)
        if hit[0] is None:
            return None, None
        return hit[0] + hit[1] * off, hit[1]

    def ribbon(pts_nrm, width, thick, taper=0.0):
        """表面に沿った帯（幅 width、厚み thick）。pts_nrm: [(点, 法線)]"""
        base = len(g.v)
        f0 = len(g.f)
        N = len(pts_nrm)
        prof = ((-0.5, 0.0), (-0.42, 1.0), (0.42, 1.0), (0.5, 0.0), (0.0, -0.3))
        for i, (p, n) in enumerate(pts_nrm):
            a = pts_nrm[max(i - 1, 0)][0]
            b = pts_nrm[min(i + 1, N - 1)][0]
            t = (b - a).normalized()
            side = n.cross(t).normalized()
            u = i / (N - 1)
            w = width * (1.0 - taper * u)
            for (sx, sy) in prof:
                g.add_vert(p + side * (w * sx) + n * (thick * sy))
        P = len(prof)
        for i in range(N - 1):
            for k in range(P):
                k2 = (k + 1) % P
                g.f.append((base + i * P + k, base + i * P + k2, base + (i + 1) * P + k2, base + (i + 1) * P + k))
        g.f.append(tuple(base + k for k in range(P)))
        g.f.append(tuple(base + (N - 1) * P + k for k in range(P)))
        cl = [p for (p, n) in pts_nrm]
        orient_outward(g, f0, lambda c: nearest_on_path(cl, c))

    # 立ち襟（前が少し開いた輪。外側は胴より太く、上へ少し広がる）
    base = len(g.v)
    f0 = len(g.f)
    arcs = [math.radians(a) for a in np.linspace(22, 338, 36)]
    sec = [(0.046, 1.345), (0.062, 1.345), (0.070, 1.432), (0.056, 1.436)]   # (半径, 高さ)：内下・外下・外上・内上
    for phi in arcs:
        d = Vector((math.sin(phi), math.cos(phi) * 0.9, 0.0))
        for (r, z) in sec:
            g.add_vert(Vector((d.x * r, d.y * r - 0.004, z)))
    S = len(sec)
    for i in range(len(arcs) - 1):
        for k in range(S):
            k2 = (k + 1) % S
            g.f.append((base + i * S + k, base + i * S + k2, base + (i + 1) * S + k2, base + (i + 1) * S + k))
    g.f.append(tuple(base + k for k in range(S)))
    g.f.append(tuple(base + (len(arcs) - 1) * S + k for k in range(S)))

    def collar_center(c):
        h = Vector((c.x, c.y + 0.004, 0.0))
        r = h.length
        mid = h.normalized() * 0.061 if r > 1e-6 else Vector()
        return Vector((mid.x, mid.y - 0.004, 1.391))
    orient_outward(g, f0, collar_center)

    # 前立て（中央の縦の線）
    pts = []
    for z in np.linspace(1.352, 0.80, 24):
        p, n = surf_y(0.0, z, 0.002)
        if p is not None:
            pts.append((p, n))
    ribbon(pts, 0.016, 0.004)
    # 裾の帯（表・上の縁・裏の縁）
    base = len(g.v)
    f0 = len(g.f)
    NP = 56
    rows = [(0.791, 0.0005), (0.791, 0.0065), (0.828, 0.0065), (0.828, 0.0005)]
    for i in range(NP):
        phi = 2 * math.pi * i / NP
        for (z, off) in rows:
            p, n = surf_radial(phi, z, off)
            g.add_vert(p)
    R = len(rows)
    for i in range(NP):
        i2 = (i + 1) % NP
        for k in range(R - 1):
            g.f.append((base + i * R + k, base + i2 * R + k, base + i2 * R + k + 1, base + i * R + k + 1))
    orient_outward(g, f0, lambda c: Vector((c.x * 0.93, c.y * 0.93, 0.8095)))
    export("ChrTrim", [g.obj("trim", "Cloth")])


# ------------------------------------------------------------------ 腰（ズボン）
def hip():
    mtb.reset()
    g = Geo()
    rows = [(0.716, 0.20, 0.16, 0.0, 2.2), (0.745, 0.275, 0.19, 0.0, 2.5), (0.79, 0.312, 0.205, 0.0, 2.6),
            (0.85, 0.306, 0.200, 0.0, 2.6), (0.905, 0.288, 0.19, 0.0, 2.6)]
    loft(g, [sell(w, d, 36, p, 0.0, y, z) for (z, w, d, y, p) in rows])
    export("ChrHip", [g.obj("hip", "Cloth")])


# ------------------------------------------------------------------ 脚・靴
# 太もも（股関節が原点）と、すね・靴（ひざ＝股関節の 0.40m 下が原点）に分ける。ひざで曲がるので座る姿が自然になる
KNEE = 0.40


def rounded_end_rings(z_end, r, direction, n=22, steps=5, ecc=1.06):
    """筒の端を半球で丸める輪（direction=-1 で下へ、+1 で上へ）"""
    rings = []
    for k in range(1, steps + 1):
        a = (math.pi / 2) * k / (steps + 0.35)
        rr = r * math.cos(a)
        z = z_end + direction * r * 0.9 * math.sin(a)
        rings.append((z, rr))
    return rings


def thigh():
    mtb.reset()
    g = Geo()
    prof = [(-0.40, 0.053), (-0.33, 0.056), (-0.20, 0.064), (-0.08, 0.070), (0.00, 0.072), (0.035, 0.064)]
    bottom = rounded_end_rings(-0.40, 0.053, -1)[::-1]
    rows = bottom + prof
    rings = [sell(2 * r, 2 * r * 1.06, 22, 2.0, 0.0, 0.0, z) for (z, r) in rows]
    loft(g, rings, cap_bottom=True, cap_top=True)
    export("ChrLeg", [g.obj("thigh", "Cloth")])


def shin():
    mtb.reset()
    g = Geo()
    # ひざが原点（ひざの少し上から足首まで）
    prof = [(-0.362, 0.050), (-0.335, 0.052), (-0.26, 0.047), (-0.16, 0.053), (-0.07, 0.055), (0.0, 0.053)]
    top = rounded_end_rings(0.0, 0.053, +1)
    rows = prof + top
    rings, cols = [], []
    for (z, r) in rows:
        knee = 0.008 * math.exp(-((z + 0.0) / 0.05) ** 2)
        rings.append(sell(2 * r, 2 * r * 1.06, 22, 2.0, 0.0, knee, z))
        cols.append((0.82, 0.82, 0.82) if z < -0.34 else (1, 1, 1))
    loft(g, rings, colors=cols)
    export("ChrShin", [g.obj("shin", "Cloth")])


def shoe():
    """靴（ひざが原点）"""
    mtb.reset()
    g = Geo()
    rows = [(-0.838, 0.084, 0.222, 0.044, 2.3), (-0.830, 0.096, 0.240, 0.045, 2.4), (-0.818, 0.100, 0.244, 0.045, 2.5),
            (-0.800, 0.098, 0.236, 0.043, 2.5), (-0.780, 0.090, 0.200, 0.030, 2.4), (-0.762, 0.078, 0.140, 0.008, 2.2),
            (-0.748, 0.070, 0.092, 0.000, 2.0)]
    rings = [sell(w, d, 26, p, 0.0, y, z + KNEE) for (z, w, d, y, p) in rows]
    cols = [(0.58, 0.58, 0.6), (0.58, 0.58, 0.6), (1, 1, 1), (1, 1, 1), (1, 1, 1), (0.9, 0.9, 0.9), (0.9, 0.9, 0.9)]
    loft(g, rings, colors=cols)
    export("ChrShoe", [g.obj("shoe", "Cloth")])


# ------------------------------------------------------------------ 腕・手（肩が原点）
def sleeve():
    mtb.reset()
    g = Geo()
    prof = [(-0.500, 0.047), (-0.468, 0.048), (-0.462, 0.044), (-0.40, 0.042), (-0.30, 0.044), (-0.27, 0.045),
            (-0.20, 0.047), (-0.10, 0.050), (-0.03, 0.051), (0.0, 0.049), (0.015, 0.040), (0.024, 0.022), (0.028, 0.006)]
    rings, cols = [], []
    for (z, r) in prof:
        fwd = 0.026 * max(0.0, (-0.27 - z) / 0.23) ** 1.2 if z < -0.27 else 0.0
        rings.append(sell(2 * r, 2 * r, 18, 2.0, 0.0, fwd, z))
        cols.append((0.80, 0.80, 0.80) if z < -0.462 else (1, 1, 1))
    loft(g, rings, colors=cols)
    export("ChrSleeve", [g.obj("sleeve", "Cloth")])


def hand():
    mtb.reset()
    g = Geo()
    rows = [(-0.632, 0.040, 0.026, 0.028, 2.2), (-0.612, 0.062, 0.036, 0.027, 2.4), (-0.575, 0.070, 0.040, 0.026, 2.5),
            (-0.542, 0.062, 0.040, 0.026, 2.3), (-0.512, 0.050, 0.040, 0.026, 2.0), (-0.492, 0.044, 0.040, 0.026, 2.0)]
    loft(g, [sell(w, d, 18, p, 0.0, y, z) for (z, w, d, y, p) in rows])
    tube_along(g, [Vector((0.0, 0.044, -0.535)), Vector((0.0, 0.054, -0.560)), Vector((0.0, 0.056, -0.582))],
               [0.0115, 0.0105, 0.008], n=10)
    export("ChrHand", [g.obj("hand", "Skin")])


# ------------------------------------------------------------------ 頭（首が原点）
HC = Vector((0.0, 0.0, 0.165))      # 頭の中心
RX, RY, RZ = 0.112, 0.114, 0.132


def head_geo():
    g = Geo()
    SEG, RINGS = 64, 40

    def deform(x, y, z):
        px, py, pz = x * RX, y * RY, z * RZ
        if z < 0:           # あご：下の方ほど細く、短く（顔の下半分を小さく）、少し前へ
            t = -z
            px *= 1.0 - 0.36 * t ** 1.5
            py *= 1.0 - 0.07 * t
            pz *= 0.88
            if y > 0:
                py += 0.010 * t * y
        if y < 0:           # 後頭部をふっくら
            py *= 1.05
        if y > 0 and z > -0.2:   # 顔の正面を少し平らに（アニメの顔立ち）
            py *= 1.0 - 0.05 * y ** 4
        return HC + Vector((px, py, pz))

    def blush(p):
        w = 0.0
        for sx in (-1, 1):
            dx, dz = (p.x - sx * 0.060) / 0.019, (p.z - 0.124) / 0.010
            w = max(w, math.exp(-(dx * dx + dz * dz)))
        if p.y < 0.05:
            w = 0.0
        k = 0.5 * w
        return (1.0 - 0.02 * k, 1.0 - 0.24 * k, 1.0 - 0.22 * k)

    top = g.add_vert(deform(0, 0, 1))
    idx = []
    for j in range(1, RINGS):
        th = math.pi * j / RINGS
        r = []
        for i in range(SEG):
            ph = 2 * math.pi * i / SEG
            p = deform(math.sin(th) * math.sin(ph), math.sin(th) * math.cos(ph), math.cos(th))
            r.append(g.add_vert(p, blush(p)))
        idx.append(r)
    bot = g.add_vert(deform(0, 0, -1))
    for i in range(SEG):
        i2 = (i + 1) % SEG
        g.f.append((top, idx[0][i], idx[0][i2]))
    for j in range(len(idx) - 1):
        for i in range(SEG):
            i2 = (i + 1) % SEG
            g.f.append((idx[j][i], idx[j + 1][i], idx[j + 1][i2], idx[j][i2]))
    for i in range(SEG):
        i2 = (i + 1) % SEG
        g.f.append((bot, idx[-1][i2], idx[-1][i]))
    orient_outward(g, 0, lambda c: HC)
    return g


def head():
    mtb.reset()
    g = head_geo()
    o = g.obj("head", "Skin")
    n = Geo()
    # 首
    loft(n, [sell(w, d, 20, 2.0, 0.0, -0.008, z) for (z, w, d) in ((-0.055, 0.078, 0.074), (0.0, 0.074, 0.070), (0.06, 0.072, 0.068), (0.10, 0.074, 0.070))],
         cap_bottom=True, cap_top=True)
    # 耳
    for sx in (-1, 1):
        b = len(n.v)
        f0 = len(n.f)
        for j in range(1, 8):
            th = math.pi * j / 8
            for i in range(12):
                ph = 2 * math.pi * i / 12
                x, y, z = math.sin(th) * math.cos(ph), math.sin(th) * math.sin(ph), math.cos(th)
                n.add_vert(Vector((sx * 0.106 + x * 0.012, -0.006 + y * 0.020, 0.150 + z * 0.028)))
        tp = n.add_vert(Vector((sx * 0.106, -0.006, 0.178)))
        bp = n.add_vert(Vector((sx * 0.106, -0.006, 0.122)))
        for i in range(12):
            i2 = (i + 1) % 12
            n.f.append((tp, b + i, b + i2))
            for j in range(6):
                n.f.append((b + j * 12 + i, b + (j + 1) * 12 + i, b + (j + 1) * 12 + i2, b + j * 12 + i2))
            n.f.append((bp, b + 6 * 12 + i2, b + 6 * 12 + i))
        ec = Vector((sx * 0.106, -0.006, 0.150))
        orient_outward(n, f0, lambda c, ec=ec: ec)
    export("ChrHead", [o, n.obj("neck", "Skin")])


# ------------------------------------------------------------------ 顔（目・まゆ・口）
def face_parts():
    mtb.reset()
    hb = head_geo().bvh()

    def onface(x, z, off):
        hit = hb.ray_cast(Vector((x, 1.0, z)), Vector((0, -1, 0)))
        if hit[0] is None:
            return Vector((x, 0.1, z)), Vector((0, 1, 0))
        return hit[0] + hit[1] * off, hit[1]

    def disc(g, pts2d, center2d, off, col_fn):
        """顔に貼る平らな多角形（中心から扇形に）"""
        f0 = len(g.f)
        c, _ = onface(center2d[0], center2d[1], off)
        ci = g.add_vert(c, col_fn(center2d))
        ids = []
        for (x, z) in pts2d:
            p, _ = onface(x, z, off)
            ids.append(g.add_vert(p, col_fn((x, z))))
        for k in range(len(ids)):
            g.f.append((ci, ids[k], ids[(k + 1) % len(ids)]))
        orient_dir(g, f0, lambda q: (q - HC).normalized())

    def band(g, pts2d, widths, off, col):
        """顔に貼る線（まつげ・まゆ・口）"""
        f0 = len(g.f)
        P = [Vector((x, 0, z)) for (x, z) in pts2d]
        ids = []
        for i, p in enumerate(P):
            t = (P[min(i + 1, len(P) - 1)] - P[max(i - 1, 0)]).normalized()
            nrm2 = Vector((-t.z, 0, t.x))
            w = widths[i]
            for s in (-0.5, 0.5):
                q = p + nrm2 * (w * s)
                v, _ = onface(q.x, q.z, off)
                ids.append(g.add_vert(v, col))
        for i in range(len(P) - 1):
            g.f.append((ids[2 * i], ids[2 * i + 1], ids[2 * i + 3], ids[2 * i + 2]))
        orient_dir(g, f0, lambda q: (q - HC).normalized())

    EYE_X, EYE_Z = 0.042, 0.150
    ES = 1.15          # 目の大きさ
    detail = Geo()
    iris = Geo()

    def upper(ex):      # 上まぶたの線（目の中心からの高さ）
        t = (ex + 0.0175) / 0.0375
        return -0.002 + 0.017 * math.sin(math.pi * min(max(t, 0), 1) ** 0.85) + 0.004 * t

    def lower(ex):
        t = (ex + 0.0175) / 0.0375
        return -0.002 - 0.0150 * math.sin(math.pi * min(max(t, 0), 1) ** 1.1) + 0.004 * t

    for sx in (-1, 1):
        def W(ex, ez, sx=sx):  # 目の座標（ex>0 が外側）→ 顔の座標
            return (sx * EYE_X + sx * ex * ES, EYE_Z + ez * ES)

        # 白目
        outline = [(ex, upper(ex)) for ex in np.linspace(-0.0175, 0.020, 14)] + \
                  [(ex, lower(ex)) for ex in np.linspace(0.020, -0.0175, 14)[1:-1]]
        disc(detail, [W(*p) for p in outline], W(0.001, 0.0), 0.0012, lambda q: (0.97, 0.97, 1.0))

        # ひとみ（上が暗く、下が明るい）と瞳孔
        def clampin(ex, ez, m=0.0006):
            return (ex, min(max(ez, lower(ex) + m), upper(ex) - m))
        ring = []
        for a in np.linspace(0, 2 * math.pi, 28, endpoint=False):
            ring.append(clampin(0.0012 + math.cos(a) * 0.0128, -0.0015 + math.sin(a) * 0.0170))
        disc(iris, [W(*p) for p in ring], W(0.0012, -0.0015), 0.0019,
             lambda q: tuple([0.40 + 0.60 * smoothstep(EYE_Z + 0.014, EYE_Z - 0.015, q[1])] * 3))
        ring = []
        for a in np.linspace(0, 2 * math.pi, 20, endpoint=False):
            ring.append(clampin(0.0012 + math.cos(a) * 0.0058, 0.0005 + math.sin(a) * 0.0085))
        disc(iris, [W(*p) for p in ring], W(0.0012, 0.0005), 0.0023, lambda q: (0.20, 0.20, 0.22))
        # 上まつげ（太い線＋目じりのはね）
        xs = list(np.linspace(-0.0185, 0.0205, 16))
        pts = [W(ex, upper(ex) + 0.0012) for ex in xs] + [W(0.0235, upper(0.0205) + 0.0035), W(0.026, upper(0.0205) + 0.0065)]
        wid = [0.0016 + 0.0034 * math.sin(math.pi * (i / (len(xs) - 1)) ** 0.7) for i in range(len(xs))] + [0.0026, 0.0012]
        band(detail, pts, [w * 1.1 for w in wid], 0.0027, (0.17, 0.11, 0.13))
        # 下まつげ（目じり側に短く）
        xs = list(np.linspace(0.006, 0.019, 6))
        band(detail, [W(ex, lower(ex) - 0.0004) for ex in xs], [0.0009 + 0.0005 * math.sin(math.pi * i / 5) for i in range(6)],
             0.0026, (0.42, 0.28, 0.28))
        # 目の光（両目とも同じ側＝光の来る側に）
        for (cx, cz, rx, rz) in ((-0.0045 * sx, 0.0055, 0.0042, 0.0052), (0.0052 * sx, -0.0085, 0.0021, 0.0021)):
            ring = [(cx + math.cos(a) * rx, cz + math.sin(a) * rz) for a in np.linspace(0, 2 * math.pi, 14, endpoint=False)]
            disc(detail, [W(*p) for p in ring], W(cx, cz), 0.0033, lambda q: (1.0, 1.0, 1.0))
        # まゆ
        xs = np.linspace(0, 1, 10)
        pts = [(sx * (0.020 + 0.046 * t), 0.188 + 0.010 * math.sin(math.pi * t ** 0.8) - 0.004 * t) for t in xs]
        band(detail, pts, [0.0040 * (1 - 0.7 * t) + 0.0008 for t in xs], 0.0026, (0.30, 0.21, 0.18))
    # 口（小さなほほえみ）
    xs = np.linspace(-1, 1, 11)
    band(detail, [(0.0115 * t, 0.101 - 0.0032 * (1 - t * t)) for t in xs], [0.0017 * (1 - 0.6 * t * t) + 0.0004 for t in xs],
         0.0024, (0.58, 0.28, 0.28))
    export("ChrFace", [detail.obj("face", "Face")])
    export("ChrEyes", [iris.obj("iris", "Iris")])


# ------------------------------------------------------------------ 髪（首が原点）
HHC = HC + Vector((0.0, -0.004, 0.008))
HRX, HRZ = 0.133, 0.146
HRY_F, HRY_B = 0.124, 0.137
T_EQ = math.radians(95)


def hair_pt(phi, th, off, hang=False, flare=0.0):
    """髪のふくらみ（頭より少し大きい楕円体）の上の点と法線。phi：前=0、+X へ回る。th：真上=0。
    hang=True のときは、横より下（th > 95°）をまっすぐ下へ垂らす"""
    def ell(phi, th):
        ry = HRY_F if math.cos(phi) >= 0 else HRY_B
        d = Vector((math.sin(th) * math.sin(phi), math.sin(th) * math.cos(phi), math.cos(th)))
        p = HHC + Vector((d.x * HRX, d.y * ry, d.z * HRZ))
        n = Vector((d.x / HRX, d.y / ry, d.z / HRZ)).normalized()
        return p, n
    if not hang or th <= T_EQ:
        p, n = ell(phi, th)
        return p + n * off, n
    p, n = ell(phi, T_EQ)
    drop = (th - T_EQ) * HRZ
    nh = Vector((n.x, n.y, 0.0)).normalized()
    q = p + nh * (off + flare * drop) - Vector((0, 0, drop))
    return q, nh


def hair_tint(p, nrm):
    """つやの輪（頭頂から 40° 前後の帯）。それ以外は少し暗め"""
    d = (p - HHC).normalized()
    th = math.degrees(math.acos(max(-1.0, min(1.0, d.z))))
    band = 1.0 - smoothstep(4.5, 10.0, abs(th - 40.0))
    outward = max(0.0, nrm.dot(d))
    k = 0.80 + 0.20 * band * smoothstep(0.2, 0.6, outward)
    return (k, k, k)


def hair_shell(g, thmax_fn, hang_fn, lip=0.010):
    """頭をおおう髪（生えぎわ thmax_fn(phi) まで）。縁は内側へ折り返して厚みを出す"""
    SEG, RS = 72, 34
    base = len(g.v)
    f0 = len(g.f)
    for i in range(SEG):
        phi = 2 * math.pi * i / SEG
        tm = thmax_fn(phi)
        hang = hang_fn(phi)
        for j in range(RS + 1):
            s = j / RS
            th = max(1e-3, s * tm)
            p, n = hair_pt(phi, th, 0.0, hang)
            g.add_vert(p, hair_tint(p, n))
        # 折り返し（縁の内側）
        p, n = hair_pt(phi, tm, 0.0, hang)
        q = p - n * lip + Vector((0, 0, lip * 0.6))
        g.add_vert(q, (0.62, 0.62, 0.62))
    R = RS + 2
    for i in range(SEG):
        i2 = (i + 1) % SEG
        for j in range(R - 1):
            g.f.append((base + i * R + j, base + i * R + j + 1, base + i2 * R + j + 1, base + i2 * R + j))
    # 縁の折り返しの面は内向き（頭の側から見える面）になるが、外から見える縁の厚みとして働く
    orient_outward(g, f0, lambda c: HHC + Vector((0, 0, -0.03)))


def lock(g, phi0, th0, phi1, th1, width, thick=0.010, lift=0.006, hang=False, flare=0.0, curl=0.0, tipin=0.0, samples=18):
    """先のとがった髪の房。根元 (phi0, th0) → 先 (phi1, th1)。lift：ふくらみ、curl：先を外へ、tipin：先を頭へ寄せる"""
    N = samples
    pts = []
    for i in range(N):
        u = i / (N - 1)
        phi = phi0 + (phi1 - phi0) * u
        th = th0 + (th1 - th0) * u
        off = 0.002 + lift * math.sin(math.pi * min(1.0, u * 1.15)) + curl * u ** 2 - tipin * u ** 2
        p, n = hair_pt(phi, th, off, hang, flare)
        pts.append((p, n, u))
    base = len(g.v)
    f0 = len(g.f)
    prof = [(-0.5, 0.0), (-0.3, 0.75), (0.0, 1.0), (0.3, 0.75), (0.5, 0.0), (0.0, -0.35)]
    for i, (p, n, u) in enumerate(pts):
        a = pts[max(i - 1, 0)][0]
        b = pts[min(i + 1, N - 1)][0]
        t = (b - a).normalized()
        side = n.cross(t).normalized()
        nn = t.cross(side).normalized()
        if nn.dot(n) < 0:
            nn = -nn
        w = width * min(1.0, 1.35 * (1.0 - u)) ** 0.85 + 0.0008
        th_ = thick * (0.45 + 0.55 * (1.0 - u)) + 0.0006
        col = hair_tint(p, nn)
        for (sx, sy) in prof:
            q = p + side * (w * sx) + nn * (th_ * sy)
            g.add_vert(q, col)
    P = len(prof)
    for i in range(N - 1):
        for k in range(P):
            k2 = (k + 1) % P
            g.f.append((base + i * P + k, base + i * P + k2, base + (i + 1) * P + k2, base + (i + 1) * P + k))
    g.f.append(tuple(base + k for k in range(P)))
    cl = [p for (p, n, u) in pts]
    orient_outward(g, f0, lambda c: nearest_on_path(cl, c))


def blend3(front, side, back):
    def f(phi):
        c = math.cos(phi)
        if c >= 0:
            return side + (front - side) * c ** 2.5
        return side + (back - side) * (-c) ** 1.2
    return f


def bangs(g, n=7, spread=50.0, th0=16.0, th1=(74.0, 80.0), width=0.042, lift=0.006):
    for i in range(n):
        t = (i / (n - 1) - 0.5) * 2 if n > 1 else 0.0
        phi0 = math.radians(t * spread * 0.85)
        phi1 = math.radians(t * spread * 1.08)
        tip = th1[i % 2] - 6.0 * abs(t) ** 2
        lock(g, phi0, math.radians(th0), phi1, math.radians(tip), width * (1 - 0.15 * abs(t)), thick=0.009, lift=lift,
             tipin=0.004)


def temple_locks(g, th1=92.0, width=0.034, phis=(36.0, 47.0)):
    """こめかみの房（前髪と横の髪のすき間をうめ、顔の輪郭にそわせる）"""
    for sx in (-1, 1):
        for k, ph in enumerate(phis):
            lock(g, math.radians(sx * (ph - 6)), math.radians(24), math.radians(sx * (ph + 3)), math.radians(th1 - 6 * k), width,
                 thick=0.008, lift=0.004, tipin=0.004)


def side_locks(g, th1=110.0, width=0.046, hang=True, phi=64.0, flare=0.05):
    for sx in (-1, 1):
        lock(g, math.radians(sx * (phi - 8)), math.radians(32), math.radians(sx * (phi + 6)), math.radians(th1), width,
             thick=0.010, lift=0.005, hang=hang, flare=flare, tipin=0.002)


def back_locks(g, th1=118.0, n=9, width=0.062, hang=False, flare=0.0, curl=0.004, span=(100, 260)):
    for i in range(n):
        a = span[0] + (span[1] - span[0]) * i / (n - 1)
        tip = th1 + (5.0 if i % 2 else -3.0)
        lock(g, math.radians(a), math.radians(14), math.radians(a + (a - 180) * 0.08), math.radians(tip), width,
             thick=0.012, lift=0.008, hang=hang, flare=flare, curl=curl)


def crown_locks(g, n=6):
    """頭頂から放射状に流れる房（つむじのまわりの立体感）"""
    for i in range(n):
        a = 360.0 * i / n + 20.0
        lock(g, math.radians(a), math.radians(4), math.radians(a + 25), math.radians(52), 0.07, thick=0.010, lift=0.007)


def hair_short():
    mtb.reset()
    g = Geo()
    hair_shell(g, blend3(math.radians(50), math.radians(90), math.radians(124)), lambda phi: False)
    crown_locks(g)
    bangs(g, n=6, spread=46, th1=(72.0, 79.0))
    temple_locks(g, th1=86.0)
    side_locks(g, th1=100.0, width=0.040, hang=False, phi=70.0)
    back_locks(g, th1=126.0, n=9, width=0.06)
    export("ChrHairShort", [g.obj("hair", "Hair")])


def hair_bob():
    mtb.reset()
    g = Geo()
    hair_shell(g, blend3(math.radians(50), math.radians(126), math.radians(132)), lambda phi: abs(math.cos(phi)) < 0.75)
    crown_locks(g)
    bangs(g, n=7, spread=50, th1=(76.0, 82.0))
    temple_locks(g, th1=100.0)
    side_locks(g, th1=132.0, width=0.050, hang=True, phi=62.0, flare=0.06)
    back_locks(g, th1=136.0, n=11, width=0.066, hang=True, flare=0.07, curl=-0.004, span=(90, 270))
    export("ChrHairBob", [g.obj("hair", "Hair")])


def hair_bun():
    mtb.reset()
    g = Geo()
    hair_shell(g, blend3(math.radians(54), math.radians(94), math.radians(120)), lambda phi: False)
    crown_locks(g, 8)
    bangs(g, n=4, spread=40, th0=20.0, th1=(62.0, 66.0), width=0.05, lift=0.004)
    temple_locks(g, th1=80.0, width=0.03)
    back_locks(g, th1=120.0, n=8, width=0.06)
    # おだんご（少しつぶした球）
    b = len(g.v)
    f0 = len(g.f)
    C = Vector((0.0, -0.112, 0.288))
    for j in range(1, 12):
        th = math.pi * j / 12
        for i in range(20):
            ph = 2 * math.pi * i / 20
            d = Vector((math.sin(th) * math.cos(ph), math.sin(th) * math.sin(ph), math.cos(th)))
            p = C + Vector((d.x * 0.054, d.y * 0.050, d.z * 0.046))
            g.add_vert(p, hair_tint(p, d) if d.z > -0.2 else (0.7, 0.7, 0.7))
    tp = g.add_vert(C + Vector((0, 0, 0.046)), (0.95, 0.95, 0.95))
    bp = g.add_vert(C - Vector((0, 0, 0.046)), (0.7, 0.7, 0.7))
    for i in range(20):
        i2 = (i + 1) % 20
        g.f.append((tp, b + i, b + i2))
        for j in range(10):
            g.f.append((b + j * 20 + i, b + (j + 1) * 20 + i, b + (j + 1) * 20 + i2, b + j * 20 + i2))
        g.f.append((bp, b + 10 * 20 + i2, b + 10 * 20 + i))
    orient_outward(g, f0, lambda c: C)
    export("ChrHairBun", [g.obj("hair", "Hair")])


def hair_pony():
    mtb.reset()
    g = Geo()
    hair_shell(g, blend3(math.radians(50), math.radians(98), math.radians(124)), lambda phi: False)
    crown_locks(g)
    bangs(g, n=7, spread=50, th1=(75.0, 82.0))
    temple_locks(g, th1=96.0)
    side_locks(g, th1=118.0, width=0.040, hang=True, phi=64.0, flare=0.03)
    back_locks(g, th1=120.0, n=9, width=0.058)
    # ポニーテール（後ろで結んで、背中へ）
    root, _ = hair_pt(math.pi, math.radians(70), 0.004)
    path = mtb.catmull([root, root + Vector((0, -0.045, -0.02)), root + Vector((0, -0.07, -0.11)),
                        root + Vector((0, -0.06, -0.22)), root + Vector((0, -0.035, -0.30))], 6)
    base = len(g.v)
    f0 = len(g.f)
    N = len(path)
    prof = ((-0.5, 0.0), (-0.3, 0.7), (0.0, 1.0), (0.3, 0.7), (0.5, 0.0), (0.3, -0.7), (0.0, -1.0), (-0.3, -0.7))
    for i, p in enumerate(path):
        u = i / (N - 1)
        t = (path[min(i + 1, N - 1)] - path[max(i - 1, 0)]).normalized()
        side = Vector((1, 0, 0))
        nn = t.cross(side).normalized()
        if nn.y > 0:
            nn = -nn
        w = 0.075 * (0.55 + 0.45 * math.sin(math.pi * min(1, u * 1.6 + 0.15))) * min(1.0, 1.4 * (1 - u)) ** 0.8 + 0.001
        th_ = 0.045 * (0.5 + 0.5 * (1 - u)) * (0.6 if u < 0.05 else 1.0)
        for (sx, sy) in prof:
            q = p + side * (w * sx) + nn * (th_ * sy * 0.5)
            g.add_vert(q, (0.86, 0.86, 0.86) if sy > 0 else (0.74, 0.74, 0.74))
    P = len(prof)
    for i in range(N - 1):
        for k in range(P):
            k2 = (k + 1) % P
            g.f.append((base + i * P + k, base + i * P + k2, base + (i + 1) * P + k2, base + (i + 1) * P + k))
    g.f.append(tuple(base + k for k in range(P)))
    orient_outward(g, f0, lambda c: nearest_on_path(path, c))
    # 結び目（暗い帯）
    tie = Geo()
    tube_along(tie, [root + Vector((0, 0.004, 0.0)), root + Vector((0, -0.018, -0.006))], [0.020, 0.020], n=12,
               col_fn=lambda u, q: (0.35, 0.35, 0.38))
    export("ChrHairPony", [g.obj("hair", "Hair"), tie.obj("tie", "Hair")])


def hair_long():
    mtb.reset()
    g = Geo()
    hair_shell(g, blend3(math.radians(50), math.radians(140), math.radians(150)), lambda phi: abs(math.cos(phi)) < 0.8)
    crown_locks(g)
    bangs(g, n=7, spread=50, th1=(76.0, 82.0))
    temple_locks(g, th1=104.0)
    side_locks(g, th1=148.0, width=0.050, hang=True, phi=62.0, flare=0.05)
    back_locks(g, th1=160.0, n=11, width=0.064, hang=True, flare=0.06, curl=0.002, span=(92, 268))
    export("ChrHairLong", [g.obj("hair", "Hair")])


# ------------------------------------------------------------------ めがね（首が原点）
def glasses():
    mtb.reset()
    hb = head_geo().bvh()
    g = Geo()
    hit = hb.ray_cast(Vector((0.043, 1.0, 0.150)), Vector((0, -1, 0)))
    yf = hit[0].y + 0.016
    for sx in (-1, 1):
        cx = sx * 0.0435
        ring = [Vector((cx + math.cos(a) * 0.0285, yf - 0.006 * (math.cos(a) * sx + 1) ** 2 * 0.5, 0.151 + math.sin(a) * 0.0225))
                for a in np.linspace(0, 2 * math.pi, 32, endpoint=False)]
        ring.append(ring[0])
        tube_along(g, ring, [0.0021] * len(ring), n=6, cap0=False, cap1=False)
        # つる（耳へ）
        a0 = Vector((sx * 0.071, yf - 0.012, 0.156))
        tube_along(g, [a0, Vector((sx * 0.114, yf - 0.04, 0.160)), Vector((sx * 0.119, -0.02, 0.159)), Vector((sx * 0.116, -0.05, 0.150))],
                   [0.0019] * 4, n=6)
    tube_along(g, [Vector((-0.016, yf, 0.157)), Vector((0.0, yf + 0.003, 0.161)), Vector((0.016, yf, 0.157))], [0.0017] * 3, n=6)
    export("ChrGlasses", [g.obj("glasses", "Cloth")])


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for fn, names in ((torso, ["ChrTorso"]), (trim, ["ChrTrim"]), (hip, ["ChrHip"]), (thigh, ["ChrLeg"]), (shin, ["ChrShin"]), (shoe, ["ChrShoe"]),
                      (sleeve, ["ChrSleeve"]), (hand, ["ChrHand"]), (head, ["ChrHead"]), (face_parts, ["ChrFace", "ChrEyes"]),
                      (hair_short, ["ChrHairShort"]), (hair_bob, ["ChrHairBob"]), (hair_bun, ["ChrHairBun"]),
                      (hair_pony, ["ChrHairPony"]), (hair_long, ["ChrHairLong"]), (glasses, ["ChrGlasses"])):
        if any(want(n) for n in names):
            fn()
