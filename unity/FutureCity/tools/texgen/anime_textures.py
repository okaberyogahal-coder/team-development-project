"""ミライフ アニメ調テクスチャ一式（すべて手続き的に生成：著作権・ライセンスの心配なし）

MT_Paving.png  … パステルの石畳（8m × 8m で 1 周期、2:1 の長方形の石を互い違いに）
MT_Lawn.png    … 芝生の地面（やわらかい筆のむら。12m で 1 周期）
MT_Wood.png    … 木の板（はちみつ色、筆で描いたような木目。4m で 1 周期）
MT_Flowers.png … 花のアトラス 2x2（0 ピンク 1 黄 2 紫(ラベンダー) 3 白）。下半分は茎と葉、上半分が花
MT_Clouds.png  … 空の積雲（4096×1024：経度 x 緯度 0〜90°、R,G=表面の向き B=厚み A=雲の量。色ではないのでリニアで取り込む）
"""
import sys, os, math, struct
import numpy as np
from PIL import Image, ImageFilter

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(2127)


def srgb(c):
    return np.asarray(c, np.float32)


def value_noise(shape, cells, seed, tile=True):
    """タイル可能なバリューノイズ（0〜1）"""
    r = np.random.default_rng(seed)
    h, w = shape
    gy, gx = cells
    g = r.random((gy + 1, gx + 1)).astype(np.float32)
    if tile:
        g[-1, :] = g[0, :]
        g[:, -1] = g[:, 0]
    ys = np.linspace(0, gy, h, endpoint=False)
    xs = np.linspace(0, gx, w, endpoint=False)
    y0 = np.floor(ys).astype(int); x0 = np.floor(xs).astype(int)
    fy = ys - y0; fx = xs - x0
    fy = fy * fy * (3 - 2 * fy); fx = fx * fx * (3 - 2 * fx)
    a = g[y0][:, x0]; b = g[y0][:, x0 + 1]; c = g[y0 + 1][:, x0]; d = g[y0 + 1][:, x0 + 1]
    top = a + (b - a) * fx[None, :]
    bot = c + (d - c) * fx[None, :]
    return top + (bot - top) * fy[:, None]


def fbm(shape, base, seed, octaves=4):
    v = np.zeros(shape, np.float32); amp = 0.5; tot = 0
    for o in range(octaves):
        v += amp * value_noise(shape, (base * 2 ** o, base * 2 ** o), seed + o * 17)
        tot += amp; amp *= 0.5
    return v / tot


def save_png(img, name):
    a = np.clip(img * 255 + 0.5, 0, 255).astype(np.uint8)
    mode = "RGBA" if a.shape[2] == 4 else "RGB"
    Image.fromarray(a, mode).save(os.path.join(OUT, name), optimize=True)
    print("saved", name, a.shape)


# ------------------------------------------------------------------ 石畳
def paving(S=2048):
    # 8m を 12 列（1 列 0.667m）、石は 2:1（横 1.333m）、行ごとに半分ずらす
    rows, cols = 12, 6
    img = np.zeros((S, S, 3), np.float32)
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    rowH = S / rows
    colW = S / cols
    palette = [srgb(c) for c in [(0.985, 0.95, 0.875), (0.97, 0.925, 0.845), (0.99, 0.80, 0.82), (0.77, 0.86, 0.99),
                                 (0.99, 0.89, 0.60), (0.75, 0.93, 0.83), (0.87, 0.81, 0.98), (0.985, 0.96, 0.92)]]
    weights = np.array([0.2, 0.14, 0.13, 0.13, 0.11, 0.1, 0.09, 0.1]); weights /= weights.sum()
    ri = np.floor(yy / rowH).astype(int)
    shift = (ri % 2) * 0.5 * colW
    xs = (xx + shift) % S
    ci = np.floor(xs / colW).astype(int)
    lx = (xs - ci * colW) / colW      # 0..1 within the slab
    ly = (yy - ri * rowH) / rowH
    tid = ri * cols + ci
    r2 = np.random.default_rng(11)
    choice = r2.choice(len(palette), size=rows * cols, p=weights)
    tint = np.array([palette[k] for k in choice])
    base = tint[tid % (rows * cols)]
    # 石ごとの明るさのゆらぎ・ゆるいグラデーション（光が当たったような）
    jitter = r2.uniform(-0.025, 0.02, rows * cols)[tid % (rows * cols)]
    grad = (0.5 - ly) * 0.035 + (lx - 0.5) * 0.01
    n = fbm((S, S), 8, 5) - 0.5
    col = base * (1 + jitter[..., None] + grad[..., None] + n[..., None] * 0.05)
    # 目地（細い明るい灰色）と、石の縁の丸み（上の縁を少し明るく、下の縁を少し暗く）
    ex = np.minimum(lx, 1 - lx) * colW
    ey = np.minimum(ly, 1 - ly) * rowH
    e = np.minimum(ex, ey)
    joint = np.clip(1 - (e - 3.0) / 2.0, 0, 1)        # 幅 ~3px
    bevel = np.clip(1 - (e - 3.0) / 9.0, 0, 1) * (1 - joint)
    topEdge = (ly < 0.5) & (ey < ex)
    col = col * (1 + np.where(topEdge, 0.05, -0.05)[..., None] * bevel[..., None])
    jointCol = srgb((0.80, 0.78, 0.76))
    col = col * (1 - joint[..., None]) + jointCol * joint[..., None]
    img = np.clip(col, 0, 1)
    save_png(img, "MT_Paving.png")


# ------------------------------------------------------------------ 芝生の地面
def lawn(S=1024):
    n1 = fbm((S, S), 6, 21)
    n2 = fbm((S, S), 24, 33)
    n3 = fbm((S, S), 64, 45)
    v = 1.0 + (n1 - 0.5) * 0.22 + (n2 - 0.5) * 0.14 + (n3 - 0.5) * 0.08
    hue = (fbm((S, S), 4, 57) - 0.5)
    base = np.stack([v * (1.0 + hue * 0.18), v, v * (1.0 - hue * 0.1)], -1)
    # 色は材質の色（緑）を掛けるので、明るさのむらだけ（白に近い）
    img = np.clip(base * 0.92, 0, 1)
    save_png(img, "MT_Lawn.png")


# ------------------------------------------------------------------ 木の板
def wood(S=1024):
    planks = 16
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    pw = S / planks
    pi_ = np.floor(xx / pw).astype(int)
    r2 = np.random.default_rng(77)
    tone = r2.uniform(0.9, 1.06, planks)[pi_]
    # 板の継ぎ目（縦方向のずれ）
    off = r2.uniform(0, S, planks)[pi_]
    seg = ((yy + off) % (S / 1.0)) / S
    grain = np.sin((xx / pw * 2.3 + fbm((S, S), 6, 3) * 3.0 + yy / S * 0.6) * 9.0) * 0.5 + 0.5
    grain = 0.96 + 0.05 * grain
    lx = (xx - pi_ * pw) / pw
    edge = np.minimum(lx, 1 - lx) * pw
    gap = np.clip(1 - (edge - 1.5) / 2.0, 0, 1)
    v = tone * grain * (1 - 0.35 * gap)
    v *= 1 + (fbm((S, S), 12, 19) - 0.5) * 0.06
    # 色は材質で掛けるので明るさのみ（わずかに暖色）
    img = np.stack([v * 1.0, v * 0.97, v * 0.93], -1)
    save_png(np.clip(img, 0, 1), "MT_Wood.png")


# ------------------------------------------------------------------ 花のアトラス
def flowers(S=1024, SS=3):
    cell = S // 2
    n = cell * SS
    out = np.zeros((S, S, 4), np.float32)
    kinds = [  # (花の色, 中心の色, 形)
        ((1.0, 0.62, 0.78), (1.0, 0.86, 0.45), "round"),
        ((1.0, 0.86, 0.30), (0.98, 0.62, 0.20), "round"),
        ((0.70, 0.56, 0.95), None, "spike"),
        ((1.0, 1.0, 0.98), (1.0, 0.82, 0.30), "daisy"),
    ]
    R = np.random.default_rng(5)
    for k, (pc, cc, shape) in enumerate(kinds):
        ys, xs = np.mgrid[0:n, 0:n].astype(np.float32) / n   # 0..1（y は下向き）
        col = np.zeros((n, n, 3), np.float32)
        alpha = np.zeros((n, n), np.float32)
        leafC = np.array((0.40, 0.70, 0.30), np.float32)
        stemC = np.array((0.34, 0.60, 0.26), np.float32)
        # 茎と葉（下半分）
        nst = 7
        tips = []
        for i in range(nst):
            x0 = 0.18 + 0.64 * (i + R.uniform(-0.3, 0.3)) / (nst - 1)
            x1 = x0 + R.uniform(-0.08, 0.08)
            ytop = R.uniform(0.18, 0.42)
            tips.append((x1, ytop))
            t = np.clip((ys - ytop) / (1.0 - ytop), 0, 1)
            cx = x1 + (x0 - x1) * t
            m = (np.abs(xs - cx) < 0.008 + 0.004 * t) & (ys >= ytop)
            col[m] = stemC * (0.85 + 0.15 * t[m, None])
            alpha[m] = 1
        for i in range(9):
            bx = R.uniform(0.15, 0.85); by = R.uniform(0.62, 0.95)
            ang = R.uniform(-1.2, 1.2) + (-math.pi / 2)
            L = R.uniform(0.12, 0.2); W = L * 0.32
            ca, sa = math.cos(ang), math.sin(ang)
            x = (xs - bx) * ca + (ys - by) * sa
            y = -(xs - bx) * sa + (ys - by) * ca
            tt = x / L
            half = W * np.clip(1 - tt * tt, 0, 1) ** 0.7
            m = (tt > -1) & (tt < 1) & (np.abs(y) < half) & (x > -L * 0.2)
            shade = 0.9 + 0.12 * (y > 0)
            col[m] = leafC * shade[m, None]
            alpha[m] = 1
        # 花（上半分）
        if shape == "spike":
            for (tx, ty) in tips:
                for j in range(9):
                    fy = ty + 0.01 - j * 0.028
                    if fy < 0.04:
                        break
                    rr = 0.026 * (1 - j / 12)
                    for s in (-1, 1):
                        fx = tx + s * rr * 0.7
                        d = np.sqrt((xs - fx) ** 2 + ((ys - fy) * 1.3) ** 2)
                        m = d < rr
                        sh = 1.0 - 0.18 * (d / rr)
                        col[m] = np.array(pc) * sh[m, None] * (0.92 + 0.08 * (j % 2))
                        alpha[m] = 1
        else:
            for (tx, ty) in tips:
                fy = max(ty - 0.02, 0.1)
                rr = R.uniform(0.085, 0.11) if shape == "round" else R.uniform(0.09, 0.12)
                d = np.sqrt((xs - tx) ** 2 + (ys - fy) ** 2)
                th = np.arctan2(ys - fy, xs - tx) + R.uniform(0, 6.28)
                petals = 5 if shape == "round" else 12
                kk = np.abs(np.cos(th * petals / 2))
                edge = rr * (0.62 + 0.38 * kk ** (0.6 if shape == "round" else 0.3))
                m = d < edge
                sh = 0.86 + 0.14 * (d / rr)
                col[m] = np.array(pc) * sh[m, None]
                alpha[m] = 1
                cm = d < rr * 0.26
                if cc is not None:
                    col[cm] = np.array(cc)
                    alpha[cm] = 1
        # 縮小
        a = alpha.reshape(cell, SS, cell, SS).mean(axis=(1, 3))
        c = (col * alpha[..., None]).reshape(cell, SS, cell, SS, 3).sum(axis=(1, 3)) / np.maximum(alpha.reshape(cell, SS, cell, SS).sum(axis=(1, 3)), 1e-6)[..., None]
        # 透明部分の色は近くの色で埋める（縁が黒くならないように）
        c = np.where(a[..., None] > 0, c, np.array(leafC))
        cy, cx = divmod(k, 2)
        out[cy * cell:(cy + 1) * cell, cx * cell:(cx + 1) * cell, :3] = c
        out[cy * cell:(cy + 1) * cell, cx * cell:(cx + 1) * cell, 3] = a
    save_png(out, "MT_Flowers.png")


# ------------------------------------------------------------------ 雲（PNG：R,G=表面の向き B=厚み A=雲の量。リニアで読む）
def clouds(W=4096, H=1024):
    """経度 u（0〜1、一周）× 緯度 v（0=地平線 1=天頂）。積雲＝泡の集まりを「なめらかな高さ」でつなぐ
    （泡と泡の境目の陰影がやわらかく、アニメの雲らしい丸いもくもくになる）"""
    lon = (np.arange(W, dtype=np.float32) + 0.5) / W * 2 * math.pi
    lat = (np.arange(H, dtype=np.float32) + 0.5) / H * (math.pi / 2)
    height = np.full((H, W), -1.0, np.float32)    # 角度（ラジアン）。負＝雲なし
    csize = np.zeros((H, W), np.float32)
    R = np.random.default_rng(42)
    up = np.array([0, 1, 0], np.float32)
    px_per_rad_x = W / (2 * math.pi)
    px_per_rad_y = H / (math.pi / 2)

    def dirs(ix0, ix1, iy0, iy1):
        lo = lon[np.arange(ix0, ix1) % W][None, :]
        la = lat[iy0:iy1][:, None]
        cl = np.cos(la)
        return np.stack([cl * np.sin(lo - math.pi) + 0 * la, np.sin(la) + 0 * lo, cl * np.cos(lo - math.pi) + 0 * la], -1)

    def cumulus(clon, clat, size):
        center = np.array([math.cos(clat) * math.sin(clon - math.pi), math.sin(clat), math.cos(clat) * math.cos(clon - math.pi)], np.float32)
        right = np.cross(up, center); right /= np.linalg.norm(right) + 1e-9
        cup = np.cross(center, right)
        squash = 0.55 + 0.45 * min(1.0, clat / math.radians(35))
        width = size * R.uniform(1.6, 2.6)
        blobs = []
        nb = R.integers(5, 9)
        for i in range(nb):
            t = (i / (nb - 1) - 0.5) * 2 if nb > 1 else 0
            ox = t * width * 0.5 + R.normal(0, size * 0.08)
            rr = size * R.uniform(0.32, 0.46) * (1.0 - 0.35 * abs(t))
            blobs.append((ox, rr * 0.55 * squash, rr))
        for i in range(R.integers(3, 7)):
            ox = R.normal(0, width * 0.22)
            rr = size * R.uniform(0.3, 0.45)
            blobs.append((ox, (size * R.uniform(0.38, 0.62) + rr * 0.3) * squash, rr))
        for i in range(R.integers(1, 4)):
            ox = R.normal(0, width * 0.12)
            rr = size * R.uniform(0.24, 0.36)
            blobs.append((ox, (size * R.uniform(0.8, 1.05) + rr * 0.2) * squash, rr))
        # 雲全体の画素の範囲
        ext_x = width * 0.5 + size * 0.6
        ext_y = size * 1.5
        iy0 = max(0, int((clat - ext_y * 0.3) * px_per_rad_y) - 2)
        iy1 = min(H, int((clat + ext_y * 1.2) * px_per_rad_y) + 2)
        if iy1 <= iy0:
            return
        span = ext_x / max(math.cos(min(clat + ext_y, 1.4)), 0.2)
        ix0 = int((clon - span) * px_per_rad_x) - 2
        ix1 = int((clon + span) * px_per_rad_x) + 2
        D = dirs(ix0, ix1, iy0, iy1)
        dv = D - center
        lx = (dv * right).sum(-1)
        ly = (dv * cup).sum(-1)
        k = size * 0.09                      # なめらかさ（大きいほど境目がやわらか）
        S = np.zeros(lx.shape, np.float64)
        for (ox, oy, rr) in blobs:
            ex = lx - ox
            ey = (ly - oy) / squash
            d2 = ex * ex + ey * ey
            h = np.sqrt(np.maximum(rr * rr - d2, 0.0))
            inside = d2 < rr * rr
            S += np.where(inside, np.exp(np.minimum(h / k, 60.0)), 0.0)
        Hc = np.where(S > 0, k * np.log(np.maximum(S, 1e-30)), -1.0).astype(np.float32)
        Hc = np.where(ly < -size * 0.06, -1.0, Hc)      # 雲の底は平ら
        cols = np.arange(ix0, ix1) % W
        sub = height[iy0:iy1][:, cols]
        better = Hc > sub
        sub = np.where(better, Hc, sub)
        cs = csize[iy0:iy1][:, cols]
        cs = np.where(better, size, cs)
        height[iy0:iy1, :][:, cols] = sub
        # 上の行は「列の並べかえ」つきの代入が効かないので、列ごとに戻す
        hh = height[iy0:iy1]
        hh[:, cols] = sub
        height[iy0:iy1] = hh
        cc = csize[iy0:iy1]
        cc[:, cols] = cs
        csize[iy0:iy1] = cc

    def lons(n):
        base = R.uniform(0, 2 * math.pi)
        return [base + (i + R.uniform(-0.3, 0.3)) / n * 2 * math.pi for i in range(n)]
    bigs = [(lo, math.radians(R.uniform(6, 18)), R.uniform(0.17, 0.25)) for lo in lons(8)]
    mids = [(lo, math.radians(R.uniform(14, 36)), R.uniform(0.08, 0.13)) for lo in lons(12)]
    smalls = [(lo, math.radians(R.uniform(26, 62)), R.uniform(0.035, 0.06)) for lo in lons(12)]
    for (clon, clat, size) in bigs + mids + smalls:
        cumulus(clon % (2 * math.pi), clat, size)

    # 高さ → 表面の向き（東向き x・上向き y の勾配から）
    Hm = np.maximum(height, 0.0)
    coslat = np.cos(lat)[:, None]
    dHx = (np.roll(Hm, -1, axis=1) - np.roll(Hm, 1, axis=1)) * 0.5 * px_per_rad_x / np.maximum(coslat, 0.05)
    dHy = np.zeros_like(Hm)
    dHy[1:-1] = (Hm[2:] - Hm[:-2]) * 0.5 * px_per_rad_y
    nx, ny, nz = -dHx, -dHy, np.ones_like(Hm)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    nx, ny, nz = nx / ln, ny / ln, nz / ln
    # 雲の量：ふちを 1 画素ほどぼかす
    edge = 1.2 / px_per_rad_y
    cov = np.clip(height / edge + 0.5, 0, 1)
    thick = np.clip(Hm / np.maximum(csize * 0.5, 1e-4), 0, 1)
    img = np.zeros((H, W, 4), np.float32)
    img[..., 0] = np.where(cov > 0, nx * 0.5 + 0.5, 0.5)
    img[..., 1] = np.where(cov > 0, ny * 0.5 + 0.5, 0.5)
    img[..., 2] = thick
    img[..., 3] = cov
    data = np.clip(img * 255 + 0.5, 0, 255).astype(np.uint8)
    # PNG は 1 行目が上（v=1）。地平線（v=0）が下になるように上下を入れかえて保存
    Image.fromarray(data[::-1], "RGBA").save(os.path.join(OUT, "MT_Clouds.png"), optimize=True)
    # 確認用（太陽が右上にあるつもりで陰影）
    L = np.array([0.5, 0.6, 0.62]); L /= np.linalg.norm(L)
    lit = np.clip(nx * L[0] + ny * L[1] + nz * L[2], 0, 1)
    sky = np.array([0.45, 0.68, 0.98])
    col = (0.72 + 0.28 * lit)[..., None] * np.array([1.0, 1.0, 1.0])
    prev = sky * (1 - cov[..., None]) + col * cov[..., None]
    Image.fromarray(np.clip(prev[::-1] * 255, 0, 255).astype(np.uint8)).resize((2048, 512)).save(os.path.join(OUT, "clouds_preview.jpg"))
    print("saved MT_Clouds.png", W, H, "coverage", float((cov > 0.5).mean()))


if __name__ == "__main__":
    which = sys.argv[2:] or ["paving", "lawn", "wood", "flowers", "clouds"]
    for w in which:
        globals()[w]()
