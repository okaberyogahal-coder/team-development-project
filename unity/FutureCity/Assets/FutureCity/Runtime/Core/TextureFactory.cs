using UnityEngine;

namespace FutureCity
{
    /// <summary>
    /// すべてのテクスチャをコードで生成する。アルベドのアルファ = 滑らかさ（Smoothness）。
    /// </summary>
    public static class TextureFactory
    {
        public struct FacadeStyle
        {
            public Color glassTop, glassBottom, frame;
            public Color[] lit;
            public float litChance;
            public float frameX, frameY;
            public float smoothGlass, smoothFrame;
            public float blinds;
            public Color edgeGlow; // 各階スラブ上端の発光ライン（黒なら無効）
        }

        static Texture2D New(string name, int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, true, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
        }

        static void Finish(Texture2D t, Color[] px)
        {
            t.SetPixels(px);
            t.Apply(true, false);
        }

        static float Fbm(float x, float y, int oct = 4)
        {
            float v = 0f, a = 0.5f, f = 1f, norm = 0f;
            for (int i = 0; i < oct; i++)
            {
                v += a * Mathf.PerlinNoise(x * f, y * f);
                norm += a;
                a *= 0.5f;
                f *= 2.03f;
            }
            return v / norm;
        }

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + s * 1442695040;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f;
            }
        }

        // ------------------------------------------------------------------ 窓ファサード
        /// <summary>1タイル = 横8窓 × 縦8階（16m × 32m）。</summary>
        public static void Facade(string name, int size, FacadeStyle s, int seed, out Texture2D albedo, out Texture2D emission)
        {
            albedo = New(name + "_Albedo", size, size);
            emission = New(name + "_Emission", size, size);
            var a = new Color[size * size];
            var e = new Color[size * size];
            var rng = new System.Random(seed);
            const int cols = 8, rows = 8;
            int cw = size / cols, ch = size / rows;

            for (int cy = 0; cy < rows; cy++)
            for (int cx = 0; cx < cols; cx++)
            {
                bool lit = rng.NextDouble() < s.litChance;
                Color lc = s.lit[rng.Next(s.lit.Length)] * (float)(0.45 + rng.NextDouble() * 0.75);
                float blind = rng.NextDouble() < s.blinds ? (float)(0.35 + rng.NextDouble() * 0.5) : 2f;
                float tint = (float)rng.NextDouble();

                for (int py = 0; py < ch; py++)
                for (int px = 0; px < cw; px++)
                {
                    int x = cx * cw + px, y = cy * ch + py, idx = y * size + x;
                    float fx = px / (float)cw, fy = py / (float)ch;
                    float n = Mathf.PerlinNoise(x * 0.05f + seed * 0.01f, y * 0.05f) * 0.1f;
                    bool frame = fx < s.frameX * 0.5f || fx > 1f - s.frameX * 0.5f || fy < s.frameY;

                    if (frame)
                    {
                        Color f = s.frame * (0.92f + n);
                        f.a = s.smoothFrame;
                        a[idx] = f;
                        bool edge = s.edgeGlow.maxColorComponent > 0f && fy < s.frameY && fy > s.frameY - 0.035f;
                        Color ec = edge ? s.edgeGlow : Color.black;
                        ec.a = 1f;
                        e[idx] = ec;
                    }
                    else
                    {
                        float gy = (fy - s.frameY) / (1f - s.frameY);
                        Color g = Color.Lerp(s.glassBottom, s.glassTop, gy) * (0.9f + n + tint * 0.08f);
                        g.a = s.smoothGlass;
                        a[idx] = g;
                        Color ec;
                        if (lit)
                        {
                            float interior = Mathf.Lerp(0.65f, 1f, gy) * (gy > blind ? 0.18f : 1f);
                            ec = lc * interior;
                        }
                        else ec = s.lit[0] * 0.012f;
                        ec.a = 1f;
                        e[idx] = ec;
                    }
                }
            }
            Finish(albedo, a);
            Finish(emission, e);
        }

        // ------------------------------------------------------------------ 道路（U=幅方向, V=進行方向）
        public static void Road(int size, out Texture2D albedo, out Texture2D emission)
        {
            albedo = New("FC_Tex_Road_Albedo", size, size);
            emission = New("FC_Tex_Road_Emission", size, size);
            var a = new Color[size * size];
            var e = new Color[size * size];
            var amber = new Color(1f, 0.62f, 0.15f);
            var white = new Color(0.9f, 0.92f, 0.95f);
            var cyan = new Color(0.2f, 0.85f, 1f);

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = Fbm(x * 0.03f, y * 0.03f);
                float grain = Hash(x, y, 7);
                float wet = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.68f, Fbm(x * 0.006f + 31f, y * 0.006f + 17f, 3)));
                Color c = new Color(0.3f, 0.31f, 0.33f) * (0.8f + 0.3f * n + 0.12f * grain);
                c = Color.Lerp(c, c * 0.9f, wet * 0.3f);
                Color em = Color.black;
                float sm = Mathf.Lerp(0.35f, 0.55f, wet);

                float dc = Mathf.Abs(u - 0.5f);
                if (dc > 0.004f && dc < 0.012f) { c = amber * 0.7f; em = amber * 0.25f; sm = 0.6f; }
                else if ((Mathf.Abs(u - 0.25f) < 0.005f || Mathf.Abs(u - 0.75f) < 0.005f) && Mathf.Repeat(v * 2f, 1f) < 0.5f)
                { c = white * 0.75f; em = white * 0.08f; sm = 0.6f; }
                else if (Mathf.Abs(u - 0.04f) < 0.005f || Mathf.Abs(u - 0.96f) < 0.005f)
                { c = cyan * 0.5f; em = cyan; sm = 0.7f; }

                c.a = sm; em.a = 1f;
                a[y * size + x] = c;
                e[y * size + x] = em;
            }
            Finish(albedo, a);
            Finish(emission, e);
        }

        // ------------------------------------------------------------------ 歩道（1タイル=8m）
        public static void Sidewalk(int size, out Texture2D albedo, out Texture2D emission)
        {
            albedo = New("FC_Tex_Sidewalk_Albedo", size, size);
            emission = New("FC_Tex_Sidewalk_Emission", size, size);
            var a = new Color[size * size];
            var e = new Color[size * size];
            int cell = size / 8;
            var cyan = new Color(0.2f, 0.85f, 1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int cx = x / cell, cy = y / cell, lx = x % cell, ly = y % cell;
                bool seam = lx < 2 || ly < 2;
                float tone = 0.78f + 0.08f * Hash(cx, cy, 3) + 0.04f * Fbm(x * 0.05f, y * 0.05f);
                Color c = seam ? new Color(0.62f, 0.6f, 0.56f) : new Color(tone, tone * 0.97f, tone * 0.92f);
                c.a = seam ? 0.3f : 0.4f + 0.25f * Fbm(x * 0.01f + 5f, y * 0.01f);
                Color em = (seam && cx % 4 == 0 && lx < 2) ? cyan * 0.6f : Color.black;
                em.a = 1f;
                a[y * size + x] = c;
                e[y * size + x] = em;
            }
            Finish(albedo, a);
            Finish(emission, e);
        }

        public static Texture2D Grass(int size)
        {
            var t = New("FC_Tex_Grass", size, size);
            var a = new Color[size * size];
            var dark = new Color(0.2f, 0.45f, 0.14f);
            var light = new Color(0.48f, 0.72f, 0.26f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Fbm(x * 0.02f, y * 0.02f);
                Color c = Color.Lerp(dark, light, n) * (0.85f + 0.3f * Hash(x, y, 11));
                c.a = 0.15f;
                a[y * size + x] = c;
            }
            Finish(t, a);
            return t;
        }

        /// <summary>やわらかな石畳（ランダムな大きさの敷石）。1タイル=8m。</summary>
        public static Texture2D Paving(int size, int seed)
        {
            var t = New("FC_Tex_Paving", size, size);
            var a = new Color[size * size];
            int cell = size / 8;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int row = y / (cell / 2);
                int xo = x + ((row & 1) == 1 ? cell / 2 : 0);
                int cx = xo / cell, lx = xo % cell, ly = y % (cell / 2);
                bool seam = lx < 2 || ly < 2;
                float tone = 0.9f + 0.1f * Hash(cx, row, seed) + 0.05f * Fbm(x * 0.03f, y * 0.03f);
                Color c = seam ? new Color(0.7f, 0.66f, 0.6f) : new Color(tone, tone * 0.95f, tone * 0.86f);
                c.a = seam ? 0.2f : 0.35f;
                a[y * size + x] = c;
            }
            Finish(t, a);
            return t;
        }

        /// <summary>金属パネル。</summary>
        public static Texture2D Panel(string name, int size, Color baseC, int seed)
        {
            var t = New(name, size, size);
            var a = new Color[size * size];
            int cell = size / 8;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int row = y / cell;
                int xo = x + ((row & 1) == 1 ? cell / 2 : 0);
                int cx = xo / cell, lx = xo % cell, ly = y % cell;
                bool seam = lx < 2 || ly < 2;
                float tone = 0.9f + 0.18f * Hash(cx, row, seed) + 0.06f * Fbm(x * 0.04f, y * 0.04f);
                Color c = seam ? baseC * 0.45f : baseC * tone;
                c.a = seam ? 0.3f : 0.55f + 0.25f * Hash(cx, row, seed + 1);
                a[y * size + x] = c;
            }
            Finish(t, a);
            return t;
        }

        /// <summary>ホログラム用の走査線テクスチャ（アルファで強弱）。</summary>
        public static Texture2D Scanlines()
        {
            const int w = 64, h = 256;
            var t = New("FC_Tex_Scanlines", w, h);
            var a = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float line = (y % 16) < 3 ? 1f : 0.38f;
                float edge = (y < 10 || y > h - 10) ? 1f : 0f;
                a[y * w + x] = new Color(1f, 1f, 1f, Mathf.Max(line, edge));
            }
            Finish(t, a);
            return t;
        }

        /// <summary>抽象的な広告ホログラム（文字なし）。</summary>
        public static Texture2D HoloAd(string name, int seed, Color colA, Color colB)
        {
            const int w = 256, h = 512;
            var t = New(name, w, h);
            var px = new Color[w * h];
            var rng = new System.Random(seed);
            float cy = 0.55f + (float)rng.NextDouble() * 0.2f;
            float ringR = 0.25f + (float)rng.NextDouble() * 0.1f;
            float[] barLen = { (float)rng.NextDouble() * 0.5f + 0.3f, (float)rng.NextDouble() * 0.5f + 0.2f, (float)rng.NextDouble() * 0.5f + 0.15f };
            Color mid = Color.Lerp(colA, colB, 0.5f);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                Color c = Color.Lerp(colB, colA, v) * 0.18f;

                float dist = Vector2.Distance(new Vector2(u, v * 2f), new Vector2(0.5f, cy * 2f));
                if (Mathf.Abs(dist - ringR) < 0.018f) c += colA;
                if (Mathf.Abs(dist - ringR * 0.72f) < 0.008f) c += colB * 0.8f;
                if (dist < ringR * 0.45f) c += colB * 0.55f * (1f - dist / (ringR * 0.45f));

                for (int b = 0; b < 3; b++)
                {
                    float by = 0.32f - b * 0.075f;
                    if (Mathf.Abs(v - by) < 0.014f && u > 0.12f && u < 0.12f + barLen[b] * 0.76f) c += mid * 0.9f;
                }
                if (Mathf.Abs(v - 0.08f) < 0.004f && u > 0.1f && u < 0.9f) c += colA * 0.6f;

                if (u < 0.02f || u > 0.98f || v < 0.01f || v > 0.99f) c += Color.white * 0.8f;
                c *= (y % 6) < 3 ? 1f : 0.62f;
                c.a = 1f;
                px[y * w + x] = c;
            }
            Finish(t, px);
            return t;
        }
    
        /// <summary>パステルカラーの石畳（白い目地、1タイル=4m）。広場の大通り・小道用。</summary>
        public static void PastelPaving(int size, out Texture2D albedo)
        {
            albedo = New("FC_Tex_PastelPaving", size, size);
            var a = new Color[size * size];
            var cols = new[]
            {
                new Color(0.98f, 0.93f, 0.7f), new Color(0.78f, 0.88f, 0.98f), new Color(0.98f, 0.82f, 0.86f),
                new Color(0.96f, 0.95f, 0.92f), new Color(0.84f, 0.94f, 0.82f), new Color(0.95f, 0.95f, 0.95f)
            };
            int cell = size / 4;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int row = y / cell;
                int xo = x + ((row & 1) == 1 ? cell / 2 : 0);
                int cx = (xo / cell) % 4, lx = xo % cell, ly = y % cell;
                bool seam = lx < 3 || ly < 3;
                Color c;
                if (seam) c = new Color(0.97f, 0.97f, 0.96f);
                else
                {
                    var bc = cols[(int)(Hash(cx, row, 5) * cols.Length) % cols.Length];
                    float n = 0.94f + 0.08f * Fbm(x * 0.03f, y * 0.03f, 3) + 0.03f * Hash(x, y, 9);
                    c = bc * n;
                }
                c.a = seam ? 0.25f : 0.38f;
                a[y * size + x] = c;
            }
            Finish(albedo, a);
        }
}
}
