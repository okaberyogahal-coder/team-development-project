"""mtb — ミライフのアセットを Blender（bpy）で作って MMSH に書き出すための道具箱

座標：Blender（Z上・右手系、メートル）で作り、書き出すときに Unity（Y上・左手系）へ (x,y,z)→(x,z,y) で変換する。
材質：マテリアル名がそのまま Unity 側の「材質スロット名」になる（MiraiMaterials が名前で材質を割り当てる）。
頂点カラー：
  "tint" … RGB の色むら（掛け算）
  "ao"   … ベイクした環境遮蔽（書き出し時に A に入る）
  "wind" … 風の効き（float、書き出し時に uv1.x）
  "extra"… 予備（uv1.y）
"""
import bpy, bmesh, math, os, random, struct
import numpy as np
from mathutils import Vector, Matrix, Euler, noise

# ------------------------------------------------------------------ シーン
def reset():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.curves, bpy.data.images, bpy.data.node_groups):
        for d in list(coll):
            try:
                coll.remove(d)
            except Exception:
                pass
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    if sc.world is None:
        sc.world = bpy.data.worlds.new("World")


def material(name, color=(0.8, 0.8, 0.8, 1.0)):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.diffuse_color = color
    return m


def assign(obj, matname):
    obj.data.materials.clear()
    obj.data.materials.append(material(matname))


def link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj


def obj_from_bmesh(name, bm, matname=None, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if smooth:
        me.shade_smooth()
    o = bpy.data.objects.new(name, me)
    link(o)
    if matname:
        assign(o, matname)
    return o


def obj_from_arrays(name, verts, faces, matname=None, smooth=True, uvs=None):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], [tuple(f) for f in faces])
    me.update()
    if uvs is not None:
        uvl = me.uv_layers.new(name="UVMap")
        # uvs: 面の頂点ごと（corner 順）
        uvl.data.foreach_set("uv", np.asarray(uvs, np.float32).ravel())
    if smooth:
        me.shade_smooth()
    o = bpy.data.objects.new(name, me)
    link(o)
    if matname:
        assign(o, matname)
    return o


def apply_modifiers(obj):
    """モディファイアを適用した形を、元のオブジェクトのメッシュとして確定する"""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=dg)
    old = obj.data
    obj.modifiers.clear()
    obj.data = me
    if old.users == 0:
        bpy.data.meshes.remove(old)
    return obj


def apply_transform(obj):
    bpy.context.view_layer.update()      # location/rotation を変えた直後は matrix_world が古いので更新する
    me = obj.data
    me.transform(obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)
    return obj


def join(objs, name=None):
    """オブジェクトをまとめて 1 つに（材質スロットは名前で保たれる）"""
    objs = [o for o in objs if o is not None]
    if len(objs) == 1:
        if name:
            objs[0].name = name
        return objs[0]
    for o in objs:
        apply_modifiers(o)
    ctx = bpy.context
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    ctx.view_layer.objects.active = objs[0]
    with ctx.temp_override(active_object=objs[0], selected_editable_objects=objs, selected_objects=objs):
        bpy.ops.object.join()
    o = ctx.view_layer.objects.active
    if name:
        o.name = name
    return o


def set_smooth_by_angle(obj, deg=40.0):
    me = obj.data
    me.shade_smooth()
    try:
        me.set_sharp_from_angle(angle=math.radians(deg))
    except Exception:
        pass


def add_bevel(obj, width, segments=3, angle=35.0, profile=0.5, clamp=True):
    m = obj.modifiers.new("Bevel", 'BEVEL')
    m.width = width
    m.segments = segments
    m.limit_method = 'ANGLE'
    m.angle_limit = math.radians(angle)
    m.profile = profile
    m.use_clamp_overlap = clamp
    m.harden_normals = False
    return m


def add_weighted_normals(obj):
    m = obj.modifiers.new("WN", 'WEIGHTED_NORMAL')
    m.keep_sharp = True
    m.weight = 50
    return m


def add_subsurf(obj, levels=2):
    m = obj.modifiers.new("Subsurf", 'SUBSURF')
    m.levels = levels
    m.render_levels = levels
    return m

# ------------------------------------------------------------------ 形
def rounded_box(name, sx, sy, sz, radius, matname, segments=4, center=(0, 0, 0)):
    """角を丸めた箱（底面は Z=center.z）"""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz + sz * 0.5)) + Vector(center)
    o = obj_from_bmesh(name, bm, matname)
    if radius > 0:
        add_bevel(o, min(radius, min(sx, sy, sz) * 0.49), segments, angle=30)
        add_weighted_normals(o)
    return o


def lathe(name, profile, matname, segments=48, center=(0, 0, 0), cap_top=False, cap_bottom=False, angle=2 * math.pi, smooth=True, recalc=False):
    """profile: [(r, z), ...] を Z 軸まわりに回した回転体。
    面の表は「profile を順にたどる向きの右側」（r を右・z を上に描いたとき）。外側→上→内側の順で外向き・上向きになる。
    recalc=True のときだけ Blender に向きを直させる（閉じた形のとき）"""
    verts, faces = [], []
    n = len(profile)
    closed = abs(angle - 2 * math.pi) < 1e-6
    segs = segments if closed else segments + 1
    for i in range(segs):
        a = angle * i / segments
        ca, sa = math.cos(a), math.sin(a)
        for (r, z) in profile:
            verts.append((center[0] + r * ca, center[1] + r * sa, center[2] + z))
    for i in range(segments):
        i2 = (i + 1) % segs if closed else i + 1
        for j in range(n - 1):
            a = i * n + j; b = i2 * n + j; c = i2 * n + j + 1; d = i * n + j + 1
            faces.append((a, b, c, d))
    if cap_bottom and profile[0][0] > 1e-5:
        faces.append(tuple(i * n for i in range(segs))[::-1])
    if cap_top and profile[-1][0] > 1e-5:
        faces.append(tuple(i * n + n - 1 for i in range(segs)))
    o = obj_from_arrays(name, verts, faces, matname, smooth)
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    if recalc:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data); bm.free()
    return o


def sweep(name, path, profile, matname, closed_path=False, closed_profile=True, twist=0.0, scale_fn=None, up=(0, 0, 1), smooth=True, caps=True):
    """path: [Vector...] に沿って profile: [(x,y)...]（断面、xは横、yは上）を流す"""
    path = [Vector(p) for p in path]
    N = len(path)
    upv = Vector(up)
    frames = []
    for i in range(N):
        if closed_path:
            t = (path[(i + 1) % N] - path[(i - 1) % N])
        else:
            t = (path[min(i + 1, N - 1)] - path[max(i - 1, 0)])
        t.normalize()
        side = t.cross(upv)
        if side.length < 1e-5:
            side = t.cross(Vector((1, 0, 0)))
        side.normalize()
        u = side.cross(t).normalized()
        frames.append((t, side, u))
    verts, faces = [], []
    P = len(profile)
    for i, p in enumerate(path):
        t, side, u = frames[i]
        s = scale_fn(i / max(N - 1, 1)) if scale_fn else 1.0
        ang = twist * i / max(N - 1, 1)
        ca, sa = math.cos(ang), math.sin(ang)
        for (x, y) in profile:
            xr, yr = x * ca - y * sa, x * sa + y * ca
            verts.append(tuple(p + side * (xr * s) + u * (yr * s)))
    rings = N if closed_path else N - 1
    for i in range(rings):
        i2 = (i + 1) % N
        for j in range(P if closed_profile else P - 1):
            j2 = (j + 1) % P
            faces.append((i * P + j, i2 * P + j, i2 * P + j2, i * P + j2))
    if caps and not closed_path and closed_profile:
        faces.append(tuple(range(P))[::-1])
        faces.append(tuple((N - 1) * P + j for j in range(P)))
    o = obj_from_arrays(name, verts, faces, matname, smooth)
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data); bm.free()
    return o


def circle_profile(r, n=12, sx=1.0, sy=1.0):
    return [(math.cos(2 * math.pi * i / n) * r * sx, math.sin(2 * math.pi * i / n) * r * sy) for i in range(n)]


def rounded_rect_profile(w, h, r, n=4):
    """幅 w・高さ h・角丸 r の断面（中心原点）"""
    pts = []
    cx, cy = w / 2 - r, h / 2 - r
    for (sx, sy, a0) in ((1, 1, 0), (-1, 1, 90), (-1, -1, 180), (1, -1, 270)):
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / n)
            pts.append((sx * cx + math.cos(a) * r, sy * cy + math.sin(a) * r))
    return pts


def uv_sphere(name, r, matname, seg=32, rings=16, center=(0, 0, 0), scale=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    for v in bm.verts:
        v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2])) + Vector(center)
    return obj_from_bmesh(name, bm, matname)


def ico_sphere(name, r, matname, subdiv=3, center=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=r)
    for v in bm.verts:
        v.co += Vector(center)
    return obj_from_bmesh(name, bm, matname)


def catmull(points, samples_per_seg=8, closed=False):
    pts = [Vector(p) for p in points]
    out = []
    n = len(pts)
    rng = range(n) if closed else range(n - 1)
    for i in rng:
        p0 = pts[(i - 1) % n] if closed else pts[max(i - 1, 0)]
        p1 = pts[i]; p2 = pts[(i + 1) % n]
        p3 = pts[(i + 2) % n] if closed else pts[min(i + 2, n - 1)]
        for k in range(samples_per_seg):
            t = k / samples_per_seg
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    if not closed:
        out.append(pts[-1])
    return out

# ------------------------------------------------------------------ 頂点データ
def ensure_color(obj, name, value=(1, 1, 1, 1), domain='POINT'):
    me = obj.data
    ca = me.color_attributes.get(name)
    if ca is None:
        ca = me.color_attributes.new(name=name, type='FLOAT_COLOR', domain=domain)
        n = len(ca.data)
        ca.data.foreach_set("color", np.tile(np.asarray(value, np.float32), n))
    return ca


def set_point_colors(obj, name, colors):
    me = obj.data
    ca = me.color_attributes.get(name)
    if ca is None or ca.domain != 'POINT':
        if ca is not None:
            me.color_attributes.remove(ca)
        ca = me.color_attributes.new(name=name, type='FLOAT_COLOR', domain='POINT')
    ca.data.foreach_set("color", np.asarray(colors, np.float32).ravel())


def set_point_float(obj, name, values):
    me = obj.data
    at = me.attributes.get(name)
    if at is None:
        at = me.attributes.new(name=name, type='FLOAT', domain='POINT')
    at.data.foreach_set("value", np.asarray(values, np.float32).ravel())


def vertex_positions(obj):
    me = obj.data
    a = np.zeros(len(me.vertices) * 3, np.float32)
    me.vertices.foreach_get("co", a)
    return a.reshape(-1, 3)


def bake_ao(objs, samples=24, distance=1.2, name="ao"):
    """Cycles で AO を頂点カラー "ao" にベイク（周りの物体も遮蔽物として効く）"""
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    try:
        sc.cycles.use_denoising = False
    except Exception:
        pass
    sc.world.light_settings.distance = distance
    for o in objs:
        apply_modifiers(o)
        me = o.data
        ca = me.color_attributes.get(name)
        if ca is None:
            ca = me.color_attributes.new(name=name, type='FLOAT_COLOR', domain='CORNER')
        me.color_attributes.active_color = ca
        if not me.materials:
            me.materials.append(material("Default"))
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    sc.render.bake.target = 'VERTEX_COLORS'
    sc.render.bake.margin = 0
    bpy.ops.object.bake(type='AO')

# ------------------------------------------------------------------ 書き出し（MMSH）
def _loop_attr(me, name, ncomp, default):
    """頂点カラー・属性を「角（ループ）ごと」の配列で返す"""
    nl = len(me.loops)
    ca = me.color_attributes.get(name)
    if ca is not None:
        n = len(ca.data)
        arr = np.zeros(n * 4, np.float32)
        ca.data.foreach_get("color", arr)
        arr = arr.reshape(-1, 4)
        if ca.domain == 'POINT':
            vi = np.zeros(nl, np.int64); me.loops.foreach_get("vertex_index", vi)
            arr = arr[vi]
        return arr[:, :ncomp]
    at = me.attributes.get(name)
    if at is not None and at.data_type == 'FLOAT':
        n = len(at.data)
        arr = np.zeros(n, np.float32)
        at.data.foreach_get("value", arr)
        if at.domain == 'POINT':
            vi = np.zeros(nl, np.int64); me.loops.foreach_get("vertex_index", vi)
            arr = arr[vi]
        elif at.domain == 'FACE':
            return None
        return arr.reshape(-1, 1)
    return np.tile(np.asarray(default, np.float32)[:ncomp], (nl, 1))


def export_mmsh(objs, path, origin=(0, 0, 0)):
    """objs をまとめて 1 つの MMSH に。材質名ごとにサブメッシュ。座標は世界座標 - origin"""
    import sys
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "preview"))
    import mmsh
    dg = bpy.context.evaluated_depsgraph_get()
    per_mat = {}
    for obj in objs:
        ev = obj.evaluated_get(dg)
        me = ev.to_mesh(preserve_all_data_layers=True, depsgraph=dg)
        me.calc_loop_triangles()
        nv, nl, nt = len(me.vertices), len(me.loops), len(me.loop_triangles)
        if nt == 0:
            ev.to_mesh_clear(); continue
        co = np.zeros(nv * 3, np.float32); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
        vi = np.zeros(nl, np.int64); me.loops.foreach_get("vertex_index", vi)
        cn = np.zeros(nl * 3, np.float32); me.corner_normals.foreach_get("vector", cn); cn = cn.reshape(-1, 3)
        if me.uv_layers.active is not None:
            uv = np.zeros(nl * 2, np.float32); me.uv_layers.active.data.foreach_get("uv", uv); uv = uv.reshape(-1, 2)
        else:
            uv = np.zeros((nl, 2), np.float32)
        tint = _loop_attr(me, "tint", 3, (1, 1, 1, 1))
        ao = _loop_attr(me, "ao", 1, (1, 1, 1, 1))
        wind = _loop_attr(me, "wind", 1, (0, 0, 0, 0))
        extra = _loop_attr(me, "extra", 1, (0, 0, 0, 0))
        tl = np.zeros(nt * 3, np.int64); me.loop_triangles.foreach_get("loops", tl); tl = tl.reshape(-1, 3)
        tm = np.zeros(nt, np.int64); me.loop_triangles.foreach_get("material_index", tm)
        M = np.array(obj.matrix_world)
        R = M[:3, :3]
        Nm = np.linalg.inv(R).T
        pw = (co @ R.T) + M[:3, 3] - np.asarray(origin)
        nw = cn @ Nm.T
        nw /= np.maximum(np.linalg.norm(nw, axis=1, keepdims=True), 1e-9)
        if np.linalg.det(R) < 0:
            tl = tl[:, [0, 2, 1]]
        names = [ (s.material.name if s.material else "Default") for s in obj.material_slots ] or ["Default"]
        loop_pos = pw[vi]
        for mi in np.unique(tm):
            name = names[min(mi, len(names) - 1)]
            sel = tl[tm == mi].ravel()
            d = per_mat.setdefault(name, {"pos": [], "nrm": [], "uv": [], "col": [], "uv1": []})
            d["pos"].append(loop_pos[sel]); d["nrm"].append(nw[sel]); d["uv"].append(uv[sel])
            col = np.concatenate([tint[sel], ao[sel]], axis=1)
            d["col"].append(col)
            d["uv1"].append(np.concatenate([wind[sel], extra[sel]], axis=1))
        ev.to_mesh_clear()

    m = mmsh.MeshData()
    allpos, allnrm, alluv, allcol, alluv1, allidx = [], [], [], [], [], []
    base = 0
    istart = 0
    for name in sorted(per_mat.keys()):
        d = per_mat[name]
        P = np.concatenate(d["pos"]); Nn = np.concatenate(d["nrm"]); U = np.concatenate(d["uv"])
        C = np.concatenate(d["col"]); U1 = np.concatenate(d["uv1"])
        # 同じ属性の角をまとめて頂点に（量子化して比較）
        key = np.concatenate([np.round(P * 2000), np.round(Nn * 300), np.round(U * 4000), np.round(C * 255), np.round(U1 * 255)], axis=1)
        uniq, first, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
        allpos.append(P[first]); allnrm.append(Nn[first]); alluv.append(U[first]); allcol.append(C[first]); alluv1.append(U1[first])
        idx = inv.ravel() + base
        allidx.append(idx)
        m.submeshes.append((istart, len(idx), name))
        istart += len(idx)
        base += len(first)
    P = np.concatenate(allpos)
    Nn = np.concatenate(allnrm)
    # Blender → Unity
    m.pos = P[:, [0, 2, 1]].astype(np.float32)
    m.nrm = Nn[:, [0, 2, 1]].astype(np.float32)
    uvs = np.concatenate(alluv).astype(np.float32)
    m.uv0 = uvs
    m.col = np.clip(np.round(np.concatenate(allcol) * 255), 0, 255).astype(np.uint8)
    m.uv1 = np.concatenate(alluv1).astype(np.float32)
    # (x,y,z)→(x,z,y) は鏡映なので、三角形の向きを逆にする（Unity の表 = cross(b-a, c-a) が法線の向き）
    m.idx = np.concatenate(allidx).astype(np.uint32).reshape(-1, 3)[:, [0, 2, 1]].ravel()
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    mmsh.write(path, m)
    print("exported %s: %d verts, %d tris, %s" % (os.path.basename(path), len(m.pos), len(m.idx) // 3,
          ", ".join("%s:%d" % (s[2], s[1] // 3) for s in m.submeshes)))
    return m
