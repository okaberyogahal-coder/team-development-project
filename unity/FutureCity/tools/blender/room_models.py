"""交流室（4 つの部屋）と庭のモデル（Blender で作って MMSH に書き出す）

  RoomPavilion  … 交流室の建物（角の丸い平面・腰壁＋大きなガラス＋白い帯・張り出した屋根と屋上緑化・入口のひさし・看板）
                   原点＝部屋の中心の床（デッキ面）。入口は前（Blender -Y ＝ Unity -Z）。色の帯・ひさしの裏・看板は "Accent"（部屋ごとに差しかえ）
  CafeSet       … 丸テーブル＋いす 3 脚（原点＝テーブルの中心の床）
  CafeCounter   … カフェのカウンター（7m、前面がゆるく曲がる）
  StudyDesk     … 机＋いす 2 脚（いすは机の後ろ＝ -Y）
  StudyBoard    … 学びの部屋の大きなボード（"Accent" の枠）
  LoungeSofa    … 曲面のソファ（前＝+Y）
  CoffeeTable   … 丸い木のローテーブル
  HobbyTable    … 作業台
  CraftShelf    … 棚と色とりどりの箱
  Easel         … イーゼルとキャンバス
  BenchGarden   … 庭のまっすぐなベンチ（前＝+Y）
  Parasol       … パラソル（"Accent" の布）
  WisteriaPergola … 藤棚（白い柱＋ゆるく弧を描く木の梁）。原点＝藤棚の中心
  RaisedBed     … 家庭菜園の木の花壇（9m×1.6m）

使い方: bvenv/bin/python room_models.py <出力フォルダ> [モデル名...]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mtb
import bpy, bmesh
import numpy as np
from mathutils import Vector
from plaza_models import finish, smooth_angle

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
ONLY = set(sys.argv[2:])
import plaza_models
plaza_models.OUT = OUT


def want(name):
    return not ONLY or name in ONLY


# ------------------------------------------------------------------ 道具
def rrect_path(hx, hy, r, step=0.6, corner_deg=7.5, gap=None, z=0.0):
    """角の丸い長方形の道すじ（上から見て反時計回り）。gap を与えると、前（y=-hy）の中央に幅 2*gap の切れ目を作り、
    右の切れ目 → … → 左の切れ目 の開いた道すじを返す"""
    pts = []

    def line(a, b):
        a, b = Vector(a), Vector(b)
        n = max(1, int(math.ceil((b - a).length / step)))
        for i in range(n):
            pts.append(a + (b - a) * (i / n))

    def arc(c, a0, a1):
        n = max(2, int(math.ceil(abs(a1 - a0) / corner_deg)))
        for i in range(n):
            a = math.radians(a0 + (a1 - a0) * i / n)
            pts.append(Vector((c[0] + r * math.cos(a), c[1] + r * math.sin(a), 0.0)))

    start = (gap if gap is not None else 0.0, -hy, 0.0)
    line(start, (hx - r, -hy, 0))
    arc((hx - r, -hy + r), -90, 0)
    line((hx, -hy + r, 0), (hx, hy - r, 0))
    arc((hx - r, hy - r), 0, 90)
    line((hx - r, hy, 0), (-hx + r, hy, 0))
    arc((-hx + r, hy - r), 90, 180)
    line((-hx, hy - r, 0), (-hx, -hy + r, 0))
    arc((-hx + r, -hy + r), 180, 270)
    if gap is not None:
        line((-hx + r, -hy, 0), (-gap, -hy, 0))
        pts.append(Vector((-gap, -hy, 0)))
    else:
        line((-hx + r, -hy, 0), (0.0, -hy, 0))
    return [p + Vector((0, 0, z)) for p in pts]


def offset_path(path, d, closed=True):
    """道すじを外側（反時計回りの右＝外）へ d だけずらす"""
    out = []
    N = len(path)
    for i in range(N):
        a = path[(i - 1) % N] if closed else path[max(i - 1, 0)]
        b = path[(i + 1) % N] if closed else path[min(i + 1, N - 1)]
        t = (b - a)
        t.z = 0
        t.normalize()
        side = Vector((t.y, -t.x, 0.0))
        out.append(path[i] + side * d)
    return out


def fan(name, center, ring, mat, up=True, uv_scale=None):
    """中心から輪へ扇形に面を張る（up=True で上向き）"""
    verts = [tuple(center)] + [tuple(p) for p in ring]
    faces = []
    n = len(ring)
    for i in range(n):
        a, b = 1 + i, 1 + (i + 1) % n
        faces.append((0, a, b) if up else (0, b, a))
    uvs = None
    if uv_scale:
        uvs = []
        for f in faces:
            for vi in f:
                v = verts[vi]
                uvs.append((v[0] / uv_scale, v[1] / uv_scale))
    o = mtb.obj_from_arrays(name, verts, faces, mat, smooth=False, uvs=uvs)
    # 上向き／下向きをそろえる
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me)
    for f in bm.faces:
        f.normal_update()
        if (f.normal.z > 0) != up:
            f.normal_flip()
    bm.to_mesh(me); bm.free()
    return o


def ring_band(name, path_in, path_out, z0, z1, mat):
    """2 本の道すじの間の水平な帯（z0 の内側 → z1 の外側）。上向き"""
    verts, faces = [], []
    N = len(path_in)
    for i in range(N):
        verts.append(tuple(path_in[i] + Vector((0, 0, z0))))
        verts.append(tuple(path_out[i] + Vector((0, 0, z1))))
    for i in range(N):
        j = (i + 1) % N
        faces.append((2 * i, 2 * i + 1, 2 * j + 1, 2 * j))
    o = mtb.obj_from_arrays(name, verts, faces, mat, smooth=True)
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me)
    for f in bm.faces:
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
    bm.to_mesh(me); bm.free()
    return o


def path_length_points(path, spacing, closed=True, skip=None):
    """道すじの上に、ほぼ等間隔の点（位置・向き）"""
    segs = []
    N = len(path)
    total = 0.0
    rng = range(N) if closed else range(N - 1)
    for i in rng:
        a, b = path[i], path[(i + 1) % N]
        L = (b - a).length
        segs.append((a, b, total, L))
        total += L
    n = max(1, int(round(total / spacing)))
    out = []
    for k in range(n):
        s = total * (k + 0.5) / n
        for (a, b, s0, L) in segs:
            if s0 <= s <= s0 + L and L > 1e-6:
                p = a + (b - a) * ((s - s0) / L)
                t = (b - a).normalized()
                if skip is None or not skip(p):
                    out.append((p, t))
                break
    return out


def place(o, loc=(0, 0, 0), rot_z=0.0, rot=None):
    if rot is not None:
        o.rotation_euler = rot
    else:
        o.rotation_euler = (0, 0, rot_z)
    o.location = loc
    mtb.apply_modifiers(o)
    mtb.apply_transform(o)
    return o


def rbox(name, sx, sy, sz, r, mat, center=(0, 0, 0), rot_z=0.0, seg=3, rot=None):
    """角の丸い箱（底面が center.z）。回転は自分の底の中心まわり"""
    o = mtb.rounded_box(name, sx, sy, sz, r, mat, segments=seg, center=(0, 0, 0))
    return place(o, center, rot_z, rot)


# ------------------------------------------------------------------ 交流室の建物
def room_pavilion():
    mtb.reset()
    objs = []
    HX, HY, R = 9.0, 8.0, 2.2          # 壁の中心線の半分の大きさ・角の丸み
    DOOR = 2.1                          # 入口の半分の幅
    FLOOR = 0.08
    WALL_TOP = 3.25
    # 床（木）と、外まわりの石の段
    floor_ring = rrect_path(HX, HY, R, step=1.0)
    objs.append(fan("floor", Vector((0, 0, FLOOR)), [p + Vector((0, 0, FLOOR)) for p in floor_ring], "Wood", up=True, uv_scale=2.4))
    plinth = mtb.sweep("plinth", rrect_path(HX, HY, R, step=0.8), [(-0.02, FLOOR - 0.001), (0.0, -0.03), (0.72, -0.03), (0.72, 0.035), (0.68, 0.075), (-0.02, 0.075)],
                       "Stone", closed_path=True, up=(0, 0, 1))
    objs.append(smooth_angle(plinth, 40))
    # 腰壁（入口で切れる）：上の縁はまるく
    wall_path = rrect_path(HX, HY, R, step=0.8, gap=DOOR)
    low = [(-0.15, FLOOR), (0.15, FLOOR), (0.15, 0.86), (0.13, 0.925), (0.08, 0.955), (-0.08, 0.955), (-0.13, 0.925), (-0.15, 0.86)]
    objs.append(smooth_angle(mtb.sweep("lowwall", wall_path, low, "Stone", up=(0, 0, 1)), 40))
    # 腰壁の上の金の線（外側）
    objs.append(mtb.sweep("goldline", wall_path, [(0.150, 0.80), (0.162, 0.805), (0.162, 0.825), (0.150, 0.83)], "Gold", up=(0, 0, 1)))
    # 大きなガラス
    objs.append(mtb.sweep("glass", wall_path, [(-0.02, 0.95), (0.02, 0.95), (0.02, 2.66), (-0.02, 2.66)], "Glass", up=(0, 0, 1)))
    # 縦の細い枠（約 2.1m ごと。入口のまわりはよける）
    closed_wall = rrect_path(HX, HY, R, step=0.4)
    for (p, t) in path_length_points(closed_wall, 2.15, skip=lambda q: q.y < -HY + 0.5 and abs(q.x) < DOOR + 0.35):
        m = mtb.rounded_box("mull", 0.09, 0.16, 1.72, 0.03, "Stone", segments=2)
        place(m, (p.x, p.y, 0.94), math.atan2(t.y, t.x))
        objs.append(m)
    # 上の白い帯（入口の上もつながる）と色の帯
    up_path = rrect_path(HX, HY, R, step=0.8)
    upper = [(-0.15, 2.65), (0.15, 2.65), (0.18, 2.70), (0.18, 3.18), (0.15, WALL_TOP), (-0.15, WALL_TOP)]
    objs.append(smooth_angle(mtb.sweep("fascia", up_path, upper, "Stone", closed_path=True, up=(0, 0, 1)), 40))
    objs.append(mtb.sweep("accent", up_path, [(0.18, 2.84), (0.20, 2.86), (0.20, 3.00), (0.18, 3.02)], "Accent", closed_path=True, up=(0, 0, 1)))
    # 入口の枠（左右の柱：内側の面は色）
    for sx in (-1, 1):
        jamb = rbox("jamb", 0.34, 0.46, 2.62, 0.06, "Stone", center=(sx * (DOOR + 0.08), -HY, FLOOR))
        objs.append(jamb)
        inner = rbox("jambc", 0.03, 0.40, 2.5, 0.01, "Accent", center=(sx * (DOOR - 0.10), -HY, FLOOR + 0.04))
        objs.append(inner)

    # 屋根：張り出し 0.9m、角の丸み R+0.9、縁は丸い鼻先、裏は木、上は緑（屋上緑化）
    OV = 0.9
    edge = rrect_path(HX + OV, HY + OV, R + OV, step=0.8)
    nose = [(-0.42, WALL_TOP), (0.0, WALL_TOP), (0.07, WALL_TOP + 0.03), (0.11, WALL_TOP + 0.10), (0.11, WALL_TOP + 0.24),
            (0.07, WALL_TOP + 0.31), (0.0, WALL_TOP + 0.34), (-0.42, WALL_TOP + 0.34)]
    objs.append(smooth_angle(mtb.sweep("roofedge", edge, nose, "Stone", closed_path=True, up=(0, 0, 1)), 45))
    objs.append(mtb.sweep("roofgold", edge, [(0.108, WALL_TOP + 0.155), (0.118, WALL_TOP + 0.16), (0.118, WALL_TOP + 0.185), (0.108, WALL_TOP + 0.19)],
                          "Gold", closed_path=True, up=(0, 0, 1)))
    soffit_ring = offset_path(edge, -0.42)
    objs.append(fan("soffit", Vector((0, 0, WALL_TOP - 0.002)), [p + Vector((0, 0, WALL_TOP - 0.002)) for p in soffit_ring], "Wood", up=False, uv_scale=2.4))
    # 屋上：縁の内側に低い立ち上がり（石）、その内側は芝（少しふくらむ）
    lip_in = offset_path(edge, -0.62)
    objs.append(ring_band("rooflip", lip_in, offset_path(edge, -0.42), WALL_TOP + 0.36, WALL_TOP + 0.34, "Stone"))
    green_ring = lip_in
    cen = Vector((0, 0, WALL_TOP + 0.46))
    objs.append(fan("roofgreen", cen, [p + Vector((0, 0, WALL_TOP + 0.36)) for p in green_ring], "Hedge", up=True))
    # 天井の丸い灯り
    for (x, y) in ((-4.5, -3.2), (0.0, -3.2), (4.5, -3.2), (-4.5, 3.2), (0.0, 3.2), (4.5, 3.2)):
        disc = mtb.lathe("light", [(0.0, -0.012), (0.34, -0.012)], "Emissive", segments=32, center=(x, y, WALL_TOP - 0.01))
        rim = mtb.lathe("lrim", [(0.33, -0.004), (0.33, -0.03), (0.40, -0.03), (0.40, -0.004)], "Gold", segments=32, center=(x, y, WALL_TOP))
        objs += [disc, rim]

    # 入口のひさし（前へ 2.2m、先は丸い）と細い柱 2 本
    CY0, CY1, CW = -HY - 0.1, -HY - 2.3, 2.9
    can = []
    can_path = []
    # ひさしの平面：後ろはまっすぐ、前は大きな角の丸み
    for p in rrect_path(CW, (CY0 - CY1) * 0.5, 1.1, step=0.4):
        can_path.append(Vector((p.x, p.y + (CY0 + CY1) * 0.5, 0)))
    cz = 2.98
    objs.append(smooth_angle(mtb.sweep("canopy", can_path, [(-0.25, cz), (0.0, cz), (0.06, cz + 0.04), (0.08, cz + 0.10), (0.06, cz + 0.16), (0.0, cz + 0.19), (-0.25, cz + 0.19)],
                                       "Stone", closed_path=True, up=(0, 0, 1)), 45))
    ci = offset_path(can_path, -0.25)
    cc = Vector((0, (CY0 + CY1) * 0.5, 0))
    objs.append(fan("canopyunder", cc + Vector((0, 0, cz + 0.002)), [p + Vector((0, 0, cz + 0.002)) for p in ci], "Accent", up=False))
    objs.append(fan("canopytop", cc + Vector((0, 0, cz + 0.20)), [p + Vector((0, 0, cz + 0.19)) for p in ci], "Stone", up=True))
    for sx in (-1, 1):
        col = mtb.lathe("col", [(0.12, 0.0), (0.12, 0.05), (0.075, 0.12), (0.06, 1.4), (0.055, cz - 0.08), (0.09, cz), (0.0, cz + 0.001)],
                        "Stone", segments=24, center=(sx * 2.45, CY1 + 0.45, FLOOR - 0.03))
        objs.append(smooth_angle(col, 40))
        objs.append(mtb.lathe("colring", [(0.062, 1.0), (0.074, 1.02), (0.074, 1.06), (0.062, 1.08)], "Gold", segments=24, center=(sx * 2.45, CY1 + 0.45, 0)))
    # 看板（屋根の縁の上、入口の真上）：白い枠＋色の面＋金の丸い紋章
    sy = -HY - OV + 0.15
    frame = rbox("signframe", 5.2, 0.16, 0.92, 0.07, "Stone", center=(0, sy, WALL_TOP + 0.34))
    objs.append(frame)
    face = rbox("signface", 4.9, 0.03, 0.66, 0.03, "Accent", center=(0, sy - 0.08, WALL_TOP + 0.47))
    objs.append(face)
    em = mtb.lathe("emblem", [(0.0, -0.03), (0.26, -0.03), (0.29, 0.0), (0.26, 0.03), (0.0, 0.03)], "Gold", segments=40)
    place(em, (-2.05, sy - 0.11, WALL_TOP + 0.80), rot=(math.pi / 2, 0, 0))
    objs.append(em)
    finish("RoomPavilion", objs, ao=True, ao_dist=1.6, samples=24, lo=0.42)


# ------------------------------------------------------------------ 家具
def chair(objs, pos, yaw):
    """丸みのあるいす（座面は布、背は木）。前＝向き yaw の方向（Blender +Y を yaw 回転）"""
    seat = rbox("seat", 0.46, 0.44, 0.10, 0.045, "Cushion", center=(0, 0, 0.40))
    frame = rbox("seatframe", 0.48, 0.46, 0.05, 0.02, "WoodDark", center=(0, 0, 0.36))
    back = rbox("back", 0.46, 0.06, 0.40, 0.03, "Wood", center=(0, -0.21, 0.46), rot=(math.radians(-8), 0, 0))
    leg = mtb.lathe("stem", [(0.18, 0.0), (0.19, 0.015), (0.16, 0.03), (0.04, 0.06), (0.035, 0.36), (0.0, 0.361)], "Stone", segments=20)
    parts = [seat, frame, back, leg]
    for o in parts:
        place(o, pos, yaw)
    objs += parts


def cafe_set():
    mtb.reset()
    objs = []
    top = mtb.lathe("top", [(0.0, 0.70), (0.70, 0.70), (0.72, 0.715), (0.72, 0.745), (0.70, 0.76), (0.0, 0.76)], "Wood", segments=48)
    objs.append(smooth_angle(top, 40))
    ped = mtb.lathe("ped", [(0.32, 0.0), (0.33, 0.02), (0.28, 0.05), (0.07, 0.12), (0.05, 0.40), (0.06, 0.66), (0.12, 0.70), (0.0, 0.701)], "Stone", segments=32)
    objs.append(smooth_angle(ped, 40))
    objs.append(mtb.lathe("gold", [(0.052, 0.40), (0.064, 0.41), (0.064, 0.44), (0.052, 0.45)], "Gold", segments=24))
    # 小さな花びん
    objs.append(mtb.lathe("vase", [(0.0, 0.76), (0.05, 0.76), (0.06, 0.80), (0.035, 0.88), (0.04, 0.90), (0.0, 0.90)], "Glass", segments=16))
    for k in range(3):
        a = math.radians(k * 120)
        # Unity では k*120° の方向（Euler(0,a,0)*forward）＝ Blender (sin a, cos a)
        d = Vector((math.sin(a), math.cos(a), 0))
        chair(objs, d * 1.12, math.atan2(-d.x, d.y) + math.pi)   # 背を外、前をテーブルへ
    finish("CafeSet", objs, ao=True, ao_dist=0.5)


def cafe_set4():
    """公園のパーゴラ・カフェ：丸テーブル＋いす 4 脚（45°＋90° ごと、中心から 1.05m）。座る人の位置に合わせる"""
    mtb.reset()
    objs = []
    top = mtb.lathe("top", [(0.0, 0.66), (0.62, 0.66), (0.64, 0.675), (0.64, 0.705), (0.62, 0.72), (0.0, 0.72)], "Wood", segments=48)
    objs.append(smooth_angle(top, 40))
    ped = mtb.lathe("ped", [(0.30, 0.0), (0.31, 0.02), (0.26, 0.05), (0.07, 0.11), (0.05, 0.38), (0.06, 0.62), (0.12, 0.66), (0.0, 0.661)], "Stone", segments=32)
    objs.append(smooth_angle(ped, 40))
    objs.append(mtb.lathe("gold", [(0.052, 0.38), (0.064, 0.39), (0.064, 0.42), (0.052, 0.43)], "Gold", segments=24))
    objs.append(mtb.lathe("vase", [(0.0, 0.72), (0.05, 0.72), (0.06, 0.76), (0.035, 0.84), (0.04, 0.86), (0.0, 0.86)], "Glass", segments=16))
    for k in range(4):
        a = math.radians(k * 90 + 45)
        # Unity では (cos a, sin a)（x, z）＝ Blender (cos a, sin a)（x, y）
        d = Vector((math.cos(a), math.sin(a), 0))
        chair(objs, d * 1.05, math.atan2(d.x, -d.y))
    finish("CafeSet4", objs, ao=True, ao_dist=0.5)


def ferry_boat():
    """湖を行く船：流線形の白い船体＋色の帯＋丸いガラスの客室＋屋根（遠景用、長さ約 26m）"""
    mtb.reset()
    objs = []
    L = 26.0
    rows = []
    for i, t in enumerate(np.linspace(0, 1, 26)):
        y = -L * 0.5 + L * t                                        # 後ろ → 前（+Y）
        bow = max(0.0, (t - 0.62) / 0.38)
        w = 8.4 * (1.0 - bow ** 1.8) ** 0.6 + 0.2
        stern = max(0.0, (0.06 - t) / 0.06)
        w *= 1.0 - 0.15 * stern
        rows.append((y, w))
    # 横断面（下が丸い船底）：断面を y ごとに作ってつなぐ
    sec = []
    for k in range(17):
        a = math.pi * k / 16          # 0〜π（右舷の上 → 船底 → 左舷の上）
        sec.append((math.cos(a), -math.sin(a)))
    verts, faces = [], []
    for (y, w) in rows:
        for (cx, cz) in sec:
            verts.append((cx * w * 0.5, y, 1.2 + cz * 1.9 * (0.55 + 0.45 * min(1.0, w / 8.4))))
    n = len(sec)
    for i in range(len(rows) - 1):
        for k in range(n - 1):
            faces.append((i * n + k, i * n + k + 1, (i + 1) * n + k + 1, (i + 1) * n + k))
    # 甲板（上のふた）
    for i in range(len(rows) - 1):
        faces.append((i * n + n - 1, i * n, (i + 1) * n, (i + 1) * n + n - 1))
    hull = mtb.obj_from_arrays("hull", verts, faces, "Stone", smooth=True)
    bm = bmesh.new(); bm.from_mesh(hull.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(hull.data); bm.free()
    objs.append(smooth_angle(hull, 50))
    # 色の帯（船体の横）
    band = [Vector((rows[i][1] * 0.5 + 0.02, rows[i][0], 0.95)) for i in range(2, 22)]
    for sx in (-1, 1):
        objs.append(mtb.sweep("band", [Vector((sx * p.x, p.y, p.z)) for p in band], mtb.rounded_rect_profile(0.06, 0.35, 0.02, n=1), "Accent", up=(0, 0, 1)))
    # 客室（丸い角のガラス）と屋根
    cab = mtb.rounded_box("cabin", 6.0, 12.0, 2.3, 0.9, "Glass", segments=4, center=(0, 0, 0))
    place(cab, (0, -1.5, 1.25))
    objs.append(cab)
    roof = mtb.rounded_box("roof", 6.8, 13.4, 0.35, 0.16, "Stone", segments=3, center=(0, 0, 0))
    place(roof, (0, -1.5, 3.55))
    objs.append(roof)
    for sx in (-1, 1):
        for y in (-7.0, -1.5, 4.0):
            objs.append(mtb.lathe("post", [(0.09, 0.0), (0.09, 2.3), (0.0, 2.31)], "Stone", segments=10, center=(sx * 3.05, y, 1.25)))
    objs.append(mtb.lathe("mast", [(0.12, 0.0), (0.08, 1.6), (0.0, 1.62)], "Stone", segments=12, center=(0, 2.0, 3.9)))
    objs.append(mtb.uv_sphere("light", 0.16, "EmissiveCool", seg=12, rings=6, center=(0, 2.0, 5.55)))
    finish("FerryBoat", objs, ao=True, ao_dist=1.5, samples=16)


def cafe_counter():
    mtb.reset()
    objs = []
    W = 7.0
    # 前面がゆるく前へふくらむ平面
    pts = []
    for t in np.linspace(-1, 1, 40):
        pts.append(Vector((t * W * 0.5, -0.45 - 0.18 * (1 - t * t), 0)))
    back = [Vector((W * 0.5, 0.45, 0)), Vector((-W * 0.5, 0.45, 0))]
    outline = pts + back
    # 押し出し（本体：木）
    bm = bmesh.new()
    lo = [bm.verts.new((p.x, p.y, 0.0)) for p in outline]
    hi = [bm.verts.new((p.x, p.y, 0.98)) for p in outline]
    bm.faces.new(lo[::-1])
    bm.faces.new(hi)
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    body = mtb.obj_from_bmesh("body", bm, "Wood", smooth=False)
    mtb.add_bevel(body, 0.04, 3, angle=30)
    objs.append(smooth_angle(body, 35))
    # 天板（白い石、少し張り出す）
    bm = bmesh.new()
    big = offset_path([Vector((p.x, p.y, 0)) for p in outline], 0.06, closed=True)
    lo = [bm.verts.new((p.x, p.y, 0.98)) for p in big]
    hi = [bm.verts.new((p.x, p.y, 1.05)) for p in big]
    bm.faces.new(lo[::-1]); bm.faces.new(hi)
    for i in range(len(big)):
        j = (i + 1) % len(big)
        bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    topo = mtb.obj_from_bmesh("top", bm, "Stone", smooth=False)
    mtb.add_bevel(topo, 0.02, 2, angle=30)
    objs.append(smooth_angle(topo, 35))
    # 前面の金の線
    gl = [p + Vector((0, -0.012, 0.86)) for p in pts]
    objs.append(mtb.sweep("gline", gl, mtb.rounded_rect_profile(0.02, 0.03, 0.008, n=1), "Gold", up=(0, 0, 1)))
    # コーヒーマシン・カップ
    objs.append(rbox("machine", 0.6, 0.45, 0.55, 0.08, "Metal", center=(1.8, 0.15, 1.05)))
    objs.append(rbox("mtop", 0.62, 0.47, 0.05, 0.02, "Gold", center=(1.8, 0.15, 1.60)))
    for i in range(4):
        objs.append(mtb.lathe("cup", [(0.0, 1.05), (0.04, 1.05), (0.05, 1.13), (0.045, 1.13), (0.0, 1.06)], "Stone", segments=16, center=(-1.6 + i * 0.22, -0.2, 0)))
    # ケーキのショーケース（ガラス）
    objs.append(rbox("case", 1.4, 0.5, 0.4, 0.06, "Glass", center=(-0.4, 0.05, 1.05)))
    finish("CafeCounter", objs, ao=True, ao_dist=0.5)


def study_desk():
    mtb.reset()
    objs = []
    objs.append(rbox("top", 2.4, 0.8, 0.06, 0.03, "Wood", center=(0, 0, 0.70)))
    for sx in (-1, 1):
        leg = rbox("leg", 0.08, 0.66, 0.70, 0.03, "Stone", center=(sx * 1.05, 0, 0.0))
        objs.append(leg)
    objs.append(rbox("modesty", 1.9, 0.03, 0.36, 0.015, "Stone", center=(0, 0.30, 0.32)))
    # 机の上：ノート（色）と小さな画面
    objs.append(rbox("book", 0.34, 0.24, 0.03, 0.01, "Accent", center=(-0.5, -0.05, 0.76)))
    objs.append(rbox("tablet", 0.42, 0.03, 0.30, 0.012, "Stone", center=(0.45, 0.12, 0.76), rot=(math.radians(-18), 0, 0)))
    objs.append(rbox("screen", 0.38, 0.012, 0.26, 0.008, "EmissiveCool", center=(0.45, 0.103, 0.785), rot=(math.radians(-18), 0, 0)))
    for sx in (-1, 1):
        chair(objs, Vector((sx * 0.6, -0.85, 0)), 0.0)
    finish("StudyDesk", objs, ao=True, ao_dist=0.5)


def study_board():
    mtb.reset()
    objs = []
    objs.append(rbox("frame", 8.2, 0.14, 2.2, 0.08, "Accent", center=(0, 0, 0.62)))
    objs.append(rbox("board", 7.7, 0.04, 1.78, 0.03, "Canvas", center=(0, -0.06, 0.83)))
    objs.append(rbox("tray", 7.4, 0.16, 0.05, 0.02, "Stone", center=(0, -0.12, 0.80)))
    for sx in (-1, 1):
        objs.append(rbox("foot", 0.12, 0.5, 0.62, 0.04, "Stone", center=(sx * 3.7, 0, 0.0)))
    # 板の上の図形（色の丸と線）
    for i, (x, z, r, m) in enumerate(((-2.6, 1.9, 0.28, "Accent1"), (-1.6, 1.5, 0.2, "Accent2"), (1.2, 1.9, 0.24, "Accent3"), (2.4, 1.35, 0.18, "Accent4"))):
        d = mtb.lathe("dot", [(0.0, 0.0), (r, 0.0)], m, segments=24)     # 外へたどる＝下向き → -90° で前（-Y）向き
        place(d, (x, -0.085, z), rot=(-math.pi / 2, 0, 0))
        objs.append(d)
    finish("StudyBoard", objs, ao=True, ao_dist=0.5)


def lounge_sofa():
    mtb.reset()
    objs = []
    R0 = 3.4          # ラウンジの円（中心のテーブルのまわり）に沿う
    half = 0.95 / R0

    def arcp(Rp, z, n=20):
        return [Vector((Rp * math.sin(t), R0 - Rp * math.cos(t), z)) for t in np.linspace(-half, half, n)]
    # 台座（木）、座面のクッション（布）、背もたれ（布）、ひじかけ
    objs.append(mtb.sweep("base", arcp(R0 + 0.02, 0.0), mtb.rounded_rect_profile(0.84, 0.24, 0.05, n=2), "WoodDark", up=(0, 0, 1)))
    objs[-1].location = (0, 0, 0.12); mtb.apply_transform(objs[-1])
    objs.append(mtb.sweep("seat", arcp(R0 - 0.04, 0.0), mtb.rounded_rect_profile(0.72, 0.18, 0.07, n=3), "Cushion", up=(0, 0, 1)))
    objs[-1].location = (0, 0, 0.33); mtb.apply_transform(objs[-1])
    objs.append(mtb.sweep("back", arcp(R0 + 0.33, 0.0), mtb.rounded_rect_profile(0.18, 0.48, 0.07, n=3), "Cushion", up=(0, 0, 1)))
    objs[-1].location = (0, 0, 0.62); mtb.apply_transform(objs[-1])
    for s in (-1, 1):
        t = s * half
        p = Vector((R0 * math.sin(t), R0 - R0 * math.cos(t), 0))
        arm = rbox("arm", 0.16, 0.86, 0.36, 0.06, "Cushion", center=(p.x, p.y, 0.24), rot_z=-t)
        objs.append(arm)
    for o in objs:
        smooth_angle(o, 40)
    finish("LoungeSofa", objs, ao=True, ao_dist=0.5)


def coffee_table():
    mtb.reset()
    objs = [smooth_angle(mtb.lathe("top", [(0.0, 0.38), (1.0, 0.38), (1.03, 0.40), (1.03, 0.43), (1.0, 0.45), (0.0, 0.45)], "Wood", segments=64), 40),
            smooth_angle(mtb.lathe("base", [(0.55, 0.0), (0.56, 0.02), (0.45, 0.05), (0.35, 0.38), (0.0, 0.381)], "Stone", segments=48), 40),
            mtb.lathe("teapot", [(0.0, 0.45), (0.10, 0.45), (0.13, 0.52), (0.11, 0.58), (0.05, 0.61), (0.02, 0.64), (0.0, 0.64)], "Stone", segments=20),
            mtb.lathe("tray", [(0.0, 0.45), (0.25, 0.45), (0.26, 0.47), (0.0, 0.47)], "Gold", segments=24, center=(0.45, -0.2, 0))]
    finish("CoffeeTable", objs, ao=True, ao_dist=0.5)


def hobby_table():
    mtb.reset()
    objs = [rbox("top", 4.0, 1.6, 0.08, 0.04, "Wood", center=(0, 0, 0.74))]
    for sx in (-1, 1):
        for sy in (-1, 1):
            objs.append(smooth_angle(mtb.lathe("leg", [(0.06, 0.0), (0.05, 0.05), (0.04, 0.74), (0.0, 0.741)], "Stone", segments=16, center=(sx * 1.8, sy * 0.65, 0)), 40))
    # 手芸かご・毛糸玉
    objs.append(mtb.lathe("basket", [(0.0, 0.82), (0.22, 0.82), (0.26, 0.95), (0.24, 0.96), (0.2, 0.86), (0.0, 0.86)], "Wood", segments=24, center=(0.9, 0.1, 0)))
    for i, m in enumerate(("Accent1", "Accent3", "Accent2")):
        objs.append(mtb.uv_sphere("yarn", 0.07, m, seg=14, rings=8, center=(0.82 + i * 0.09, 0.08 + (i % 2) * 0.07, 0.93)))
    objs.append(rbox("cloth", 1.0, 0.7, 0.01, 0.005, "Accent4", center=(-0.9, 0.0, 0.82)))
    finish("HobbyTable", objs, ao=True, ao_dist=0.5)


def craft_shelf():
    mtb.reset()
    objs = []
    W, D, H = 10.0, 0.6, 2.2
    objs.append(rbox("back", W, 0.06, H, 0.03, "Wood", center=(0, D * 0.5 - 0.03, 0)))
    for z in (0.0, 0.72, 1.44, 2.14):
        objs.append(rbox("shelf", W, D, 0.06, 0.02, "Stone" if z in (0.0, 2.14) else "Wood", center=(0, 0, z)))
    for x in np.linspace(-W * 0.5 + 0.03, W * 0.5 - 0.03, 6):
        objs.append(rbox("side", 0.06, D, H, 0.02, "Stone", center=(x, 0, 0)))
    rnd = np.random.default_rng(5)
    mats = ["Accent1", "Accent2", "Accent3", "Accent4", "Canvas", "Wood"]
    for z in (0.06, 0.78, 1.50):
        x = -W * 0.5 + 0.2
        while x < W * 0.5 - 0.3:
            w = float(rnd.uniform(0.25, 0.6))
            h = float(rnd.uniform(0.3, 0.55))
            if abs(((x + 5.0) % 2.0) - 0.0) < 0.05:
                x += 0.2
                continue
            m = mats[int(rnd.integers(0, len(mats)))]
            objs.append(rbox("box", w * 0.9, D * 0.8, h, 0.03, m, center=(x + w * 0.5, -0.02, z)))
            x += w + float(rnd.uniform(0.03, 0.2))
    finish("CraftShelf", objs, ao=True, ao_dist=0.6)


def easel():
    mtb.reset()
    objs = []
    for sx in (-1, 1):
        objs.append(mtb.sweep("leg", [Vector((sx * 0.38, -0.12, 0.0)), Vector((sx * 0.12, 0.02, 1.75))], mtb.circle_profile(0.025, 8), "Wood", up=(0, 1, 0)))
    objs.append(mtb.sweep("back", [Vector((0, 0.45, 0.0)), Vector((0, 0.04, 1.62))], mtb.circle_profile(0.022, 8), "Wood", up=(1, 0, 0)))
    objs.append(rbox("ledge", 0.9, 0.08, 0.04, 0.015, "Wood", center=(0, -0.10, 0.82)))
    tilt = math.radians(-8)          # 上を後ろへ傾ける
    objs.append(rbox("canvas", 0.86, 0.04, 0.66, 0.015, "Canvas", center=(0, -0.08, 0.86), rot=(tilt, 0, 0)))
    # キャンバスの絵（色の丸）：キャンバスの前の面（自分の y=-0.02）に貼る
    for (x, hgt, r, m) in ((-0.15, 0.46, 0.10, "Accent2"), (0.18, 0.22, 0.14, "Accent4"), (-0.05, 0.16, 0.08, "Accent1")):
        y0, z0 = -0.024, hgt
        y1 = y0 * math.cos(tilt) - z0 * math.sin(tilt)
        z1 = y0 * math.sin(tilt) + z0 * math.cos(tilt)
        d = mtb.lathe("paint", [(0.0, 0.0), (r, 0.0)], m, segments=20)
        place(d, (x, -0.08 + y1, 0.86 + z1), rot=(-math.pi / 2 + tilt, 0, 0))
        objs.append(d)
    finish("Easel", objs, ao=True, ao_dist=0.4)


# ------------------------------------------------------------------ 庭
def bench_garden():
    mtb.reset()
    objs = []
    L = 2.2
    for i, off in enumerate((-0.21, -0.105, 0.0, 0.105, 0.21)):
        pts = [Vector((x, off, 0.43)) for x in np.linspace(-L * 0.5, L * 0.5, 6)]
        objs.append(mtb.sweep("slat%d" % i, pts, mtb.rounded_rect_profile(0.088, 0.04, 0.012, n=2), "Wood", up=(0, 0, 1)))
    for i, (off, z) in enumerate(((-0.285, 0.58), (-0.31, 0.70), (-0.335, 0.82))):
        pts = [Vector((x, off, z)) for x in np.linspace(-L * 0.5 * 0.97, L * 0.5 * 0.97, 6)]
        objs.append(mtb.sweep("back%d" % i, pts, mtb.rounded_rect_profile(0.04, 0.085, 0.012, n=2), "Wood", up=(0, 0, 1)))
    for x in (-0.88, 0.88):
        bm = bmesh.new()
        prof2d = [(-0.27, 0.0), (0.27, 0.0), (0.27, 0.06), (0.24, 0.40), (0.20, 0.42), (-0.20, 0.42), (-0.30, 0.46),
                  (-0.36, 0.90), (-0.41, 0.90), (-0.36, 0.42), (-0.30, 0.06)]
        verts = [bm.verts.new((x - 0.055, y, z)) for (y, z) in prof2d] + [bm.verts.new((x + 0.055, y, z)) for (y, z) in prof2d]
        n = len(prof2d)
        bm.faces.new([verts[i] for i in range(n)][::-1])
        bm.faces.new([verts[n + i] for i in range(n)])
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new([verts[i], verts[j], verts[n + j], verts[n + i]])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        o = mtb.obj_from_bmesh("leg", bm, "Stone", smooth=False)
        mtb.add_bevel(o, 0.035, segments=3, angle=30)
        mtb.apply_modifiers(o)
        objs.append(smooth_angle(o, 35))
        objs.append(mtb.uv_sphere("cap", 0.035, "Gold", seg=16, rings=8, center=(x * 1.2, -0.22, 0.46)))
    finish("BenchGarden", objs, ao=True, ao_dist=0.6)


def parasol():
    mtb.reset()
    objs = []
    objs.append(smooth_angle(mtb.lathe("base", [(0.30, 0.0), (0.31, 0.03), (0.24, 0.08), (0.06, 0.12), (0.0, 0.121)], "Stone", segments=32), 40))
    objs.append(mtb.lathe("pole", [(0.035, 0.1), (0.035, 2.95), (0.0, 2.951)], "Stone", segments=12))
    # 布（8 枚の山がある傘）：外→内へたどると表は上
    n = 96
    verts, faces = [], []
    rings = [(1.65, 2.18), (1.45, 2.33), (1.05, 2.56), (0.55, 2.76), (0.08, 2.88)]
    for (r, z) in rings:
        for i in range(n):
            a = 2 * math.pi * i / n
            k = 1.0 - 0.06 * (0.5 + 0.5 * math.cos(8 * a)) * (r / 1.65)
            dz = 0.05 * (0.5 + 0.5 * math.cos(8 * a)) * (r / 1.65)
            verts.append((r * k * math.cos(a), r * k * math.sin(a), z + dz))
    for j in range(len(rings) - 1):
        for i in range(n):
            i2 = (i + 1) % n
            faces.append((j * n + i, j * n + i2, (j + 1) * n + i2, (j + 1) * n + i))
    top = len(verts); verts.append((0, 0, 2.90))
    for i in range(n):
        faces.append(((len(rings) - 1) * n + i, (len(rings) - 1) * n + (i + 1) % n, top))
    can = mtb.obj_from_arrays("canopy", verts, faces, "Accent", smooth=True)
    me = can.data
    bm = bmesh.new(); bm.from_mesh(me)
    for f in bm.faces:
        f.normal_update()
        if f.normal.z < 0:
            f.normal_flip()
    bm.to_mesh(me); bm.free()
    objs.append(can)
    # 裏（白）
    verts2 = [(v[0] * 0.995, v[1] * 0.995, v[2] - 0.012) for v in verts]
    under = mtb.obj_from_arrays("under", verts2, faces, "Canvas", smooth=True)
    me = under.data
    bm = bmesh.new(); bm.from_mesh(me)
    for f in bm.faces:
        f.normal_update()
        if f.normal.z > 0:
            f.normal_flip()
    bm.to_mesh(me); bm.free()
    objs.append(under)
    objs.append(mtb.uv_sphere("finial", 0.06, "Gold", seg=16, rings=8, center=(0, 0, 2.97)))
    finish("Parasol", objs, ao=True, ao_dist=0.6)


def wisteria_pergola():
    """藤棚：白い石の柱 6 本＋ゆるく弧を描く 2 本の大梁＋細い横木（藤の花は Unity 側で吊るす）"""
    mtb.reset()
    objs = []
    for ix in (-1, 0, 1):
        for iz in (-1, 1):
            x, y = ix * 5.0, iz * 3.0
            col = mtb.lathe("col", [(0.22, 0.0), (0.23, 0.04), (0.17, 0.12), (0.13, 0.3), (0.12, 2.5), (0.15, 2.66), (0.19, 2.72), (0.0, 2.73)],
                            "Stone", segments=24, center=(x, y, 0))
            objs.append(smooth_angle(col, 40))
            objs.append(mtb.lathe("ring", [(0.125, 0.9), (0.14, 0.92), (0.14, 0.97), (0.125, 0.99)], "Gold", segments=24, center=(x, y, 0)))
    for iz in (-1, 1):
        y = iz * 3.0
        pts = [Vector((x, y, 2.78 + 0.22 * (1 - (x / 6.2) ** 2))) for x in np.linspace(-6.2, 6.2, 40)]
        objs.append(smooth_angle(mtb.sweep("beam", pts, mtb.rounded_rect_profile(0.20, 0.28, 0.06, n=2), "Wood", up=(0, 1, 0)), 40))
    for x in np.linspace(-5.4, 5.4, 13):
        zc = 2.78 + 0.22 * (1 - (x / 6.2) ** 2) + 0.2
        pts = [Vector((x, y, zc + 0.06 * (1 - (y / 3.8) ** 2))) for y in np.linspace(-3.8, 3.8, 16)]
        objs.append(mtb.sweep("slat", pts, mtb.rounded_rect_profile(0.10, 0.12, 0.035, n=2), "WoodDark", up=(1, 0, 0)))
    finish("WisteriaPergola", objs, ao=True, ao_dist=0.8)


def raised_bed():
    """家庭菜園の木の花壇（9m × 1.6m、高さ 0.6m、土の上面）"""
    mtb.reset()
    objs = []
    W, D, H = 9.0, 1.6, 0.6
    body = rbox("box", W, D, H, 0.08, "Wood", center=(0, 0, 0))
    objs.append(smooth_angle(body, 35))
    cap = rbox("cap", W + 0.08, D + 0.08, 0.06, 0.03, "Stone", center=(0, 0, H - 0.02))
    objs.append(cap)
    soil = rbox("soil", W - 0.24, D - 0.24, 0.05, 0.02, "Soil", center=(0, 0, H - 0.01))
    objs.append(soil)
    finish("RaisedBed", objs, ao=True, ao_dist=0.6)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    table = [("RoomPavilion", room_pavilion), ("CafeSet", cafe_set), ("CafeSet4", cafe_set4), ("FerryBoat", ferry_boat), ("CafeCounter", cafe_counter), ("StudyDesk", study_desk),
             ("StudyBoard", study_board), ("LoungeSofa", lounge_sofa), ("CoffeeTable", coffee_table), ("HobbyTable", hobby_table),
             ("CraftShelf", craft_shelf), ("Easel", easel), ("BenchGarden", bench_garden), ("Parasol", parasol),
             ("WisteriaPergola", wisteria_pergola), ("RaisedBed", raised_bed)]
    for name, fn in table:
        if want(name):
            fn()
