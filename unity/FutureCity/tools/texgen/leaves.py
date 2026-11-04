"""葉のアトラス（2x2）：0=広葉の房A 1=広葉の房B 2=桜の花の房 3=細かい葉（糸杉・生け垣）
RGB はほぼ白の明るさのゆらぎだけ（色は材質と頂点カラーで決める）、A が形。
"""
import sys, math
import numpy as np
from PIL import Image

S = 1024          # 出力サイズ
CELL = S // 2
SS = 3            # スーパーサンプリング

def leaf_mask(X, Y, cx, cy, L, W, ang):
    """先のとがった葉（レンズ形）"""
    ca, sa = math.cos(ang), math.sin(ang)
    x = (X - cx) * ca + (Y - cy) * sa
    y = -(X - cx) * sa + (Y - cy) * ca
    t = x / L                         # -1〜1
    half = W * np.clip(1 - t * t, 0, 1) ** 0.8 * (1.0 - 0.25 * t)   # 根元が少し太い
    inside = (np.abs(t) <= 1) & (np.abs(y) <= half)
    # 葉の中の明るさ：中央の葉脈が少し明るく、縁がやや暗い
    shade = 1.0 - 0.10 * np.clip(np.abs(y) / np.maximum(half, 1e-6), 0, 1) ** 2 + 0.05 * (np.abs(y) < W * 0.06)
    return inside, shade

def petal_mask(X, Y, cx, cy, r, ang):
    """5 枚の花びらの花（桜）"""
    dx, dy = X - cx, Y - cy
    rr = np.sqrt(dx * dx + dy * dy)
    th = np.arctan2(dy, dx) - ang
    # 5 弁：r(θ) = r * (0.55 + 0.45 |cos(2.5θ)|)、花びらの先に切れ込み
    k = np.abs(np.cos(2.5 * th))
    notch = 1.0 - 0.18 * np.exp(-((np.mod(th * 5 / (2 * np.pi), 1.0) - 0.5) ** 2) / 0.004)
    edge = r * (0.5 + 0.5 * k ** 0.6) * notch
    inside = rr <= edge
    shade = 0.92 + 0.08 * np.clip(rr / np.maximum(edge, 1e-6), 0, 1) - 0.12 * (rr < r * 0.18)
    return inside, shade

def make_cell(kind, rng):
    n = CELL * SS
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32) / n * 2 - 1     # -1〜1
    alpha = np.zeros((n, n), np.float32)
    lum = np.full((n, n), 0.9, np.float32)
    if kind in (0, 1):
        count = 34 if kind == 0 else 44
        for i in range(count):
            # 房の形：中心ほど密、外周は葉先が飛び出す
            r = math.sqrt(rng.random()) * (0.62 if kind == 0 else 0.66)
            a = rng.random() * 2 * math.pi
            cx, cy = r * math.cos(a), r * math.sin(a) * 0.92
            L = rng.uniform(0.17, 0.27) * (1.15 if kind == 1 else 1.0)
            W = L * rng.uniform(0.38, 0.5)
            ang = a + rng.uniform(-0.7, 0.7)          # 外向き
            inside, shade = leaf_mask(xs, ys, cx + math.cos(ang) * L * 0.6, cy + math.sin(ang) * L * 0.6, L, W, ang)
            v = rng.uniform(0.8, 1.0)
            lum = np.where(inside, v * shade, lum)
            alpha = np.maximum(alpha, inside.astype(np.float32))
    elif kind == 2:
        # 桜：小さな花をたくさん（かたまりの中心ほど密）
        for i in range(95):
            r = math.sqrt(rng.random()) * 0.68
            a = rng.random() * 2 * math.pi
            cx, cy = r * math.cos(a), r * math.sin(a)
            inside, shade = petal_mask(xs, ys, cx, cy, rng.uniform(0.07, 0.1), rng.random() * 6.28)
            v = rng.uniform(0.86, 1.0)
            lum = np.where(inside, v * shade, lum)
            alpha = np.maximum(alpha, inside.astype(np.float32))
        # すき間に小さな若葉
        for i in range(10):
            r = math.sqrt(rng.random()) * 0.55
            a = rng.random() * 2 * math.pi
            inside, shade = leaf_mask(xs, ys, r * math.cos(a), r * math.sin(a), 0.11, 0.045, a)
            lum = np.where(inside & (alpha < 0.5), 0.55 * shade, lum)
            alpha = np.maximum(alpha, inside.astype(np.float32))
    else:
        for i in range(220):
            r = math.sqrt(rng.random()) * 0.7
            a = rng.random() * 2 * math.pi
            cx, cy = r * math.cos(a), r * math.sin(a)
            inside, shade = leaf_mask(xs, ys, cx, cy, rng.uniform(0.07, 0.11), rng.uniform(0.022, 0.035), a + rng.uniform(-0.5, 0.5))
            v = rng.uniform(0.78, 1.0)
            lum = np.where(inside, v * shade, lum)
            alpha = np.maximum(alpha, inside.astype(np.float32))
    # 中心は必ず不透明（内側の塊に使う）
    core = (xs * xs + ys * ys) < 0.18 ** 2
    alpha = np.where(core, 1.0, alpha)
    # 縮小（アンチエイリアス）
    a = alpha.reshape(CELL, SS, CELL, SS).mean(axis=(1, 3))
    l = (lum * alpha).reshape(CELL, SS, CELL, SS).sum(axis=(1, 3)) / np.maximum(alpha.reshape(CELL, SS, CELL, SS).sum(axis=(1, 3)), 1e-6)
    l = np.where(a > 0, l, 0.9)
    return l, a

def main(out):
    rng = np.random.default_rng(7)
    import random
    R = random.Random(11)
    img = np.zeros((S, S, 4), np.float32)
    for k in range(4):
        l, a = make_cell(k, R)
        cy, cx = divmod(k, 2)
        img[cy * CELL:(cy + 1) * CELL, cx * CELL:(cx + 1) * CELL, 0] = l
        img[cy * CELL:(cy + 1) * CELL, cx * CELL:(cx + 1) * CELL, 1] = l
        img[cy * CELL:(cy + 1) * CELL, cx * CELL:(cx + 1) * CELL, 2] = l
        img[cy * CELL:(cy + 1) * CELL, cx * CELL:(cx + 1) * CELL, 3] = a
    # 画像は上が v=1（Unity の UV と同じ向き：PNG の 1 行目が v=1）
    Image.fromarray(np.clip(img * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA").save(out)
    print("saved", out)

if __name__ == "__main__":
    main(sys.argv[1])
