using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// 手続き的メッシュ構築ヘルパー。
    /// UV はワールド座標基準で計算するため、建物の大きさに関わらず窓テクスチャが正しい密度で並ぶ。
    /// 1つのビルダーに複数マテリアル（サブメッシュ）を積み、最後に1オブジェクトへまとめる。
    /// </summary>
    public class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>(8192);
        readonly List<Vector3> norms = new List<Vector3>(8192);
        readonly List<Vector2> uvs = new List<Vector2>(8192);
        readonly SortedDictionary<int, List<int>> subs = new SortedDictionary<int, List<int>>();

        public bool IsEmpty => verts.Count == 0;
        public int VertexCount => verts.Count;
        /// <summary>接線を計算する（法線マップを使う素材に必要。草のカードなど不要なものは false で軽くする）</summary>
        public bool computeTangents = true;

        /// <summary>
        /// 0 より大きいと、AddBox が角を丸めた（面取りした）箱になる（ベンチ・植え込み・壁などに柔らかなハイライトが出る）。
        /// 広場を作るあいだだけ設定し、終わったら 0 に戻す。
        /// </summary>
        public static float BevelSize = 0f;

        List<int> Tris(int slot)
        {
            if (!subs.TryGetValue(slot, out var list))
            {
                list = new List<int>(1024);
                subs[slot] = list;
            }
            return list;
        }

        // ---------------------------------------------------------------- primitives

        /// <summary>a=左下, b=右下, c=右上, d=左上（外側から見た並び）。法線は自動計算。</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvMin, Vector2 uvMax, int slot)
        {
            Vector3 n = Vector3.Cross(d - a, b - a);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - b, b - a);
            n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            AddQuadN(a, b, c, d, n, n, n, n, uvMin, uvMax, slot);
        }

        public void AddQuadN(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                             Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
                             Vector2 uvMin, Vector2 uvMax, int slot)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            norms.Add(na); norms.Add(nb); norms.Add(nc); norms.Add(nd);
            uvs.Add(new Vector2(uvMin.x, uvMin.y));
            uvs.Add(new Vector2(uvMax.x, uvMin.y));
            uvs.Add(new Vector2(uvMax.x, uvMax.y));
            uvs.Add(new Vector2(uvMin.x, uvMax.y));
            var t = Tris(slot);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
        }

        /// <summary>水平なキャップ（凸多角形・XZ平面で反時計回りの頂点列）。</summary>
        public void AddCap(IList<Vector3> ring, bool facingUp, float tile, int slot)
        {
            Vector3 center = Vector3.zero;
            for (int k = 0; k < ring.Count; k++) center += ring[k];
            center /= ring.Count;
            Vector3 n = facingUp ? Vector3.up : Vector3.down;

            int ci = verts.Count;
            verts.Add(center); norms.Add(n); uvs.Add(new Vector2(center.x, center.z) / tile);
            for (int k = 0; k < ring.Count; k++)
            {
                verts.Add(ring[k]); norms.Add(n); uvs.Add(new Vector2(ring[k].x, ring[k].z) / tile);
            }
            var t = Tris(slot);
            int cnt = ring.Count;
            for (int k = 0; k < cnt; k++)
            {
                int a = ci + 1 + k, b = ci + 1 + (k + 1) % cnt;
                if (facingUp) { t.Add(ci); t.Add(b); t.Add(a); }
                else { t.Add(ci); t.Add(a); t.Add(b); }
            }
        }

        /// <summary>
        /// 多角形の押し出し（柱・箱・円柱・錐台）。bottom/top は同数の頂点、XZ平面で反時計回り。
        /// </summary>
        public void AddExtrusion(Vector3 basePos, IList<Vector2> bottom, IList<Vector2> top, float height, float yawDeg,
                                 int sideSlot, int capSlot, Vector2 tile,
                                 bool smooth = false, bool capTop = true, bool capBottom = false, bool snapWindows = true)
        {
            int n = bottom.Count;
            var rot = Quaternion.Euler(0f, yawDeg, 0f);
            var b = new Vector3[n];
            var tp = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                b[i] = basePos + rot * new Vector3(bottom[i].x, 0f, bottom[i].y);
                tp[i] = basePos + rot * new Vector3(top[i].x, height, top[i].y);
            }

            float v0 = basePos.y / tile.y, v1 = (basePos.y + height) / tile.y;

            float uScale = 1f / tile.x;
            if (smooth && snapWindows)
            {
                float perim = 0f;
                for (int i = 0; i < n; i++) perim += Vector3.Distance(b[i], b[(i + 1) % n]);
                if (perim > 0.001f) uScale = Mathf.Max(0.125f, Mathf.Round(perim / tile.x * 8f) / 8f) / perim;
            }

            float u = 0f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float len = Vector3.Distance(b[i], b[j]);
                if (smooth)
                {
                    Vector3 ni = Flat(b[i] - basePos), nj = Flat(b[j] - basePos);
                    AddQuadN(b[i], b[j], tp[j], tp[i], ni, nj, nj, ni,
                             new Vector2(u * uScale, v0), new Vector2((u + len) * uScale, v1), sideSlot);
                    u += len;
                }
                else
                {
                    float uw = len / tile.x;
                    if (snapWindows) uw = Mathf.Max(0.125f, Mathf.Round(uw * 8f) / 8f);
                    AddQuad(b[i], b[j], tp[j], tp[i], new Vector2(0f, v0), new Vector2(uw, v1), sideSlot);
                }
            }

            if (capTop) AddCap(tp, true, tile.x, capSlot);
            if (capBottom) AddCap(b, false, tile.x, capSlot);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward;
        }

        /// <summary>底面中心 basePos、サイズ size の箱（Y軸回転可）。BevelSize &gt; 0 なら角を面取りする。</summary>
        public void AddBox(Vector3 basePos, Vector3 size, float yaw, int sideSlot, int capSlot, Vector2 tile, bool capBottom = false)
        {
            float bev = Mathf.Min(BevelSize, 0.22f * Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
            if (bev >= 0.004f)
            {
                AddBeveledBox(basePos, size, yaw, sideSlot, capSlot, tile, capBottom, bev);
                return;
            }
            var pts = Rect(size.x, size.z);
            AddExtrusion(basePos, pts, pts, size.y, yaw, sideSlot, capSlot, tile, false, true, capBottom);
        }

        static Vector3 BoxPt(Vector3 basePos, Quaternion rot, Vector3 local, float y)
        {
            return basePos + rot * new Vector3(local.x, y, local.z);
        }

        static Vector2 Planar(Vector3 wp, Vector2 tile) => new Vector2(wp.x, wp.z) / tile.x;

        /// <summary>
        /// 角を面取りした箱。側面と上面は平らな法線、面取り部分は法線をなめらかに変えて丸みのあるハイライトにする。
        /// 下面を作らない場合は下の角も面取りしない（地面に置く物は見えないため）。
        /// </summary>
        public void AddBeveledBox(Vector3 basePos, Vector3 size, float yaw, int sideSlot, int capSlot, Vector2 tile, bool capBottom, float b)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            float hx = size.x * 0.5f, hz = size.z * 0.5f, h = size.y;
            float y0 = capBottom ? b : 0f, y1 = h - b;
            Vector3[] N = { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
            float[] E = { hx, hz, hx, hz };
            Vector3 up = Vector3.up;
            var topRing = new Vector3[4];
            var botRing = new Vector3[4];
            for (int k = 0; k < 4; k++)
            {
                int kp = (k + 1) % 4, km = (k + 3) % 4;
                Vector3 nk = rot * N[k], nkp = rot * N[kp];
                Vector3 p0 = N[k] * E[k] + N[km] * (E[km] - b);
                Vector3 p1 = N[k] * E[k] + N[kp] * (E[kp] - b);
                Vector3 q1 = N[kp] * E[kp] + N[k] * (E[k] - b);
                Vector3 corner = N[k] * (E[k] - b) + N[kp] * (E[kp] - b);
                Vector3 i0 = p0 - N[k] * b, i1 = p1 - N[k] * b;
                float len = Vector3.Distance(p0, p1);

                // 側面
                var uvA = new Vector2(b / tile.x, (basePos.y + y0) / tile.y);
                var uvB = new Vector2((b + len) / tile.x, (basePos.y + y1) / tile.y);
                AddQuadOriented(BoxPt(basePos, rot, p0, y0), BoxPt(basePos, rot, p1, y0), BoxPt(basePos, rot, p1, y1), BoxPt(basePos, rot, p0, y1),
                                nk, nk, nk, nk, uvA, uvB, sideSlot, nk);
                // 縦の角（次の面へ丸く回り込む）
                AddQuadOriented(BoxPt(basePos, rot, p1, y0), BoxPt(basePos, rot, q1, y0), BoxPt(basePos, rot, q1, y1), BoxPt(basePos, rot, p1, y1),
                                nk, nkp, nkp, nk, new Vector2(0f, uvA.y), new Vector2(b * 1.414f / tile.x, uvB.y), sideSlot, (nk + nkp).normalized);
                // 上の面取り（側面 → 上面）
                Vector3 a0 = BoxPt(basePos, rot, p0, y1), a1 = BoxPt(basePos, rot, p1, y1);
                Vector3 c0 = BoxPt(basePos, rot, i0, h), c1 = BoxPt(basePos, rot, i1, h);
                AddQuadNUV(a0, a1, c1, c0, nk, nk, up, up, Planar(a0, tile), Planar(a1, tile), Planar(c1, tile), Planar(c0, tile), capSlot, (nk + up).normalized);
                // 上の角（三角形）
                Vector3 ta = BoxPt(basePos, rot, p1, y1), tb = BoxPt(basePos, rot, q1, y1), tc = BoxPt(basePos, rot, corner, h);
                AddTriOriented(ta, tb, tc, nk, nkp, up, Planar(ta, tile), Planar(tb, tile), Planar(tc, tile), capSlot, (nk + nkp + up).normalized);
                topRing[k] = BoxPt(basePos, rot, corner, h);

                if (capBottom)
                {
                    Vector3 d0 = BoxPt(basePos, rot, p0, y0), d1 = BoxPt(basePos, rot, p1, y0);
                    Vector3 e0 = BoxPt(basePos, rot, i0, 0f), e1 = BoxPt(basePos, rot, i1, 0f);
                    AddQuadNUV(d0, d1, e1, e0, nk, nk, -up, -up, Planar(d0, tile), Planar(d1, tile), Planar(e1, tile), Planar(e0, tile), sideSlot, (nk - up).normalized);
                    Vector3 ba = BoxPt(basePos, rot, p1, y0), bb = BoxPt(basePos, rot, q1, y0), bc = BoxPt(basePos, rot, corner, 0f);
                    AddTriOriented(ba, bb, bc, nk, nkp, -up, Planar(ba, tile), Planar(bb, tile), Planar(bc, tile), sideSlot, (nk + nkp - up).normalized);
                    botRing[k] = BoxPt(basePos, rot, corner, 0f);
                }
            }
            // 上面
            AddQuadNUV(topRing[0], topRing[1], topRing[2], topRing[3], up, up, up, up,
                       Planar(topRing[0], tile), Planar(topRing[1], tile), Planar(topRing[2], tile), Planar(topRing[3], tile), capSlot, up);
            if (capBottom)
                AddQuadNUV(botRing[0], botRing[1], botRing[2], botRing[3], -up, -up, -up, -up,
                           Planar(botRing[0], tile), Planar(botRing[1], tile), Planar(botRing[2], tile), Planar(botRing[3], tile), sideSlot, -up);
        }

        /// <summary>頂点ごとの UV を指定する四角形（向きは expected 側に自動でそろえる）</summary>
        public void AddQuadNUV(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
                               Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, int slot, Vector3 expected)
        {
            bool flip = Vector3.Dot(Vector3.Cross(d - a, b - a), expected) < 0f;
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            norms.Add(na); norms.Add(nb); norms.Add(nc); norms.Add(nd);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            var t = Tris(slot);
            if (!flip) { t.Add(i); t.Add(i + 3); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 1); }
            else { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        }

        /// <summary>三角形（Unity の表面は時計回り。expected 側を向くように並びを自動でそろえる）</summary>
        public void AddTriOriented(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc,
                                   Vector2 ua, Vector2 ub, Vector2 uc, int slot, Vector3 expected)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            norms.Add(na); norms.Add(nb); norms.Add(nc);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
            var t = Tris(slot);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), expected) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
        }

        /// <summary>
        /// 両面で使う板（葉・花・草のカード）。center を中心に right・up の半分の長さで広がる。
        /// 法線は頂点ごとに指定（樹冠の外向きにすると、ふんわり立体的に照らされる）。
        /// </summary>
        public void AddCard(Vector3 center, Vector3 halfRight, Vector3 halfUp, Vector2 uvMin, Vector2 uvMax, int slot,
                            Vector3 n0, Vector3 n1, Vector3 n2, Vector3 n3)
        {
            AddQuadN(center - halfRight - halfUp, center + halfRight - halfUp, center + halfRight + halfUp, center - halfRight + halfUp,
                     n0, n1, n2, n3, uvMin, uvMax, slot);
        }

        /// <summary>リング（ドーナツ型の帯）。ネオンリングやホログラムに使用。</summary>
        public void AddRing(Vector3 c, float rIn, float rOut, float height, int segs, int slot)
        {
            Vector3 up = Vector3.up * height;
            float tileU = 2f * Mathf.PI * rOut / 8f;
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * Mathf.PI * 2f / segs, a1 = (i + 1) * Mathf.PI * 2f / segs;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 o0 = c + d0 * rOut, o1 = c + d1 * rOut, i0 = c + d0 * rIn, i1 = c + d1 * rIn;
                var uvMin = new Vector2(i / (float)segs * tileU, 0f);
                var uvMax = new Vector2((i + 1) / (float)segs * tileU, 1f);
                AddQuadN(o0, o1, o1 + up, o0 + up, d0, d1, d1, d0, uvMin, uvMax, slot);       // 外壁
                AddQuadN(i1, i0, i0 + up, i1 + up, -d1, -d0, -d0, -d1, uvMin, uvMax, slot);   // 内壁
                AddQuad(o0 + up, o1 + up, i1 + up, i0 + up, uvMin, uvMax, slot);             // 上面
                AddQuad(o1, o0, i0, i1, uvMin, uvMax, slot);                                 // 下面
            }
        }

        /// <summary>地面に沿った上向きの帯（道路）。U=幅方向 0..1、V=長さ方向。</summary>
        public void AddGroundStrip(Vector3 p0, Vector3 p1, float halfWidth, float vTile, float vOffset, int slot)
        {
            Vector3 t = p1 - p0;
            float len = t.magnitude;
            t /= len;
            Vector3 right = Vector3.Cross(Vector3.up, t);
            AddQuad(p0 - right * halfWidth, p0 + right * halfWidth, p1 + right * halfWidth, p1 - right * halfWidth,
                    new Vector2(0f, vOffset / vTile), new Vector2(1f, (vOffset + len) / vTile), slot);
        }

        /// <summary>経路に沿った角柱（高架軌道など）。path の y は基準高さ。</summary>
        public void AddBeam(IList<Vector3> path, bool closed, float lateral, float halfWidth, float yBottom, float height,
                            int sideSlot, int topSlot, int bottomSlot)
        {
            int n = path.Count;
            int segs = closed ? n : n - 1;
            var L = new Vector3[n];
            var R = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = path[closed ? (i - 1 + n) % n : Mathf.Max(i - 1, 0)];
                Vector3 next = path[closed ? (i + 1) % n : Mathf.Min(i + 1, n - 1)];
                Vector3 t = next - prev; t.y = 0f; t.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, t);
                Vector3 c = path[i] + right * lateral;
                c.y = path[i].y + yBottom;
                L[i] = c - right * halfWidth;
                R[i] = c + right * halfWidth;
            }

            Vector3 up = Vector3.up * height;
            float s = 0f;
            for (int k = 0; k < segs; k++)
            {
                int i = k, j = (k + 1) % n;
                float len = Vector3.Distance(path[i], path[j]);
                var uvA = new Vector2(s / 8f, 0f);
                var uvB = new Vector2((s + len) / 8f, 1f);
                AddQuad(R[i], R[j], R[j] + up, R[i] + up, uvA, uvB, sideSlot);         // 右側面
                AddQuad(L[j], L[i], L[i] + up, L[j] + up, uvA, uvB, sideSlot);         // 左側面
                AddQuad(L[i] + up, R[i] + up, R[j] + up, L[j] + up, uvA, uvB, topSlot); // 上面
                AddQuad(R[i], L[i], L[j], R[j], uvA, uvB, bottomSlot);                 // 底面
                s += len;
            }
        }

        // ---------------------------------------------------------------- shapes

        public static Vector2[] Rect(float w, float d)
        {
            return new[]
            {
                new Vector2(-w * 0.5f, -d * 0.5f), new Vector2(w * 0.5f, -d * 0.5f),
                new Vector2(w * 0.5f, d * 0.5f), new Vector2(-w * 0.5f, d * 0.5f)
            };
        }

        public static Vector2[] Circle(float r, int segs, float phaseDeg = 0f)
        {
            var pts = new Vector2[segs];
            for (int i = 0; i < segs; i++)
            {
                float a = phaseDeg * Mathf.Deg2Rad + i * Mathf.PI * 2f / segs;
                pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return pts;
        }

        // ---------------------------------------------------------------- curved shapes（曲面）

        /// <summary>法線が expected 側を向くように、必要なら並びを反転して四角形を追加する。</summary>
        public void AddQuadOriented(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                                    Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd,
                                    Vector2 uvMin, Vector2 uvMax, int slot, Vector3 expected)
        {
            if (Vector3.Dot(Vector3.Cross(d - a, b - a), expected) >= 0f)
                AddQuadN(a, b, c, d, na, nb, nc, nd, uvMin, uvMax, slot);
            else
                AddQuadN(b, a, d, c, nb, na, nd, nc, uvMin, uvMax, slot);
        }

        /// <summary>
        /// 球・楕円体（なめらかな法線）。
        /// fromLat：下端の緯度（-90=全球、0=上半分のドーム）。lonGap：入口などで空ける経度の範囲（度）。
        /// bumpy＞0 で表面を波打たせる（木の葉のかたまり用）。
        /// </summary>
        public void AddSphere(Vector3 c, Vector3 radii, int lat, int lon, int slot,
                              float fromLat = -90f, float bumpy = 0f, int seed = 0,
                              float gapFromDeg = 0f, float gapToDeg = 0f, bool inside = false)
        {
            lat = Mathf.Max(2, lat);
            lon = Mathf.Max(3, lon);
            var P = new Vector3[lat + 1, lon + 1];
            var N = new Vector3[lat + 1, lon + 1];
            float so = seed * 0.731f;
            for (int i = 0; i <= lat; i++)
            {
                float phi = Mathf.Lerp(fromLat, 90f, i / (float)lat) * Mathf.Deg2Rad;
                for (int j = 0; j <= lon; j++)
                {
                    float th = j / (float)lon * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(phi) * Mathf.Cos(th), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(th));
                    float k = 1f;
                    if (bumpy > 0f)
                    {
                        int jj = j == lon ? 0 : j;   // 継ぎ目をそろえる
                        float th2 = jj / (float)lon * Mathf.PI * 2f;
                        float n1 = Mathf.PerlinNoise(Mathf.Cos(th2) * 1.7f + so, phi * 1.9f + so * 0.5f);
                        float n2 = Mathf.PerlinNoise(Mathf.Sin(th2) * 2.3f - so, phi * 2.6f + 3.1f);
                        k = 1f + bumpy * ((n1 + n2) - 1f) * Mathf.Cos(phi);   // 極では0にして、面が折り返らないようにする
                    }
                    P[i, j] = c + Vector3.Scale(dir, radii) * k;
                    var nrm = new Vector3(dir.x / Mathf.Max(radii.x, 1e-4f), dir.y / Mathf.Max(radii.y, 1e-4f), dir.z / Mathf.Max(radii.z, 1e-4f)).normalized;
                    N[i, j] = inside ? -nrm : nrm;
                }
            }
            for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                if (gapToDeg > gapFromDeg)
                {
                    float midDeg = (j + 0.5f) / lon * 360f;
                    if (midDeg > gapFromDeg && midDeg < gapToDeg) continue;
                }
                var uvMin = new Vector2(j / (float)lon * 4f, i / (float)lat * 2f);
                var uvMax = new Vector2((j + 1) / (float)lon * 4f, (i + 1) / (float)lat * 2f);
                // 経度は反時計回り、緯度は下から上なので、この並びで外向きになる（内側から見る場合は反転）
                if (!inside)
                    AddQuadN(P[i, j], P[i, j + 1], P[i + 1, j + 1], P[i + 1, j],
                             N[i, j], N[i, j + 1], N[i + 1, j + 1], N[i + 1, j], uvMin, uvMax, slot);
                else
                    AddQuadN(P[i, j + 1], P[i, j], P[i + 1, j], P[i + 1, j + 1],
                             N[i, j + 1], N[i, j], N[i + 1, j], N[i + 1, j + 1], uvMin, uvMax, slot);
            }
        }

        /// <summary>トーラス（輪）。rot で向きを変えられる（既定は水平の輪）。</summary>
        public void AddTorus(Vector3 c, Quaternion rot, float R, float r, int segU, int segV, int slot,
                             float fromDeg = 0f, float toDeg = 360f)
        {
            segU = Mathf.Max(3, segU);
            segV = Mathf.Max(3, segV);
            for (int i = 0; i < segU; i++)
            {
                float u0 = Mathf.Lerp(fromDeg, toDeg, i / (float)segU) * Mathf.Deg2Rad;
                float u1 = Mathf.Lerp(fromDeg, toDeg, (i + 1) / (float)segU) * Mathf.Deg2Rad;
                for (int j = 0; j < segV; j++)
                {
                    float v0 = j / (float)segV * Mathf.PI * 2f, v1 = (j + 1) / (float)segV * Mathf.PI * 2f;
                    var na = TorusN(rot, u0, v0);
                    AddQuadOriented(TorusP(c, rot, R, r, u0, v0), TorusP(c, rot, R, r, u1, v0), TorusP(c, rot, R, r, u1, v1), TorusP(c, rot, R, r, u0, v1),
                                    na, TorusN(rot, u1, v0), TorusN(rot, u1, v1), TorusN(rot, u0, v1),
                                    new Vector2(i / (float)segU, j / (float)segV), new Vector2((i + 1) / (float)segU, (j + 1) / (float)segV),
                                    slot, (na + TorusN(rot, u1, v1)).normalized);
                }
            }
        }

        static Vector3 TorusP(Vector3 c, Quaternion rot, float R, float r, float u, float v) =>
            c + rot * new Vector3((R + r * Mathf.Cos(v)) * Mathf.Cos(u), r * Mathf.Sin(v), (R + r * Mathf.Cos(v)) * Mathf.Sin(u));
        static Vector3 TorusN(Quaternion rot, float u, float v) =>
            rot * new Vector3(Mathf.Cos(v) * Mathf.Cos(u), Mathf.Sin(v), Mathf.Cos(v) * Mathf.Sin(u));

        /// <summary>折れ線に沿った円管（手すり・つる・電線など）。</summary>
        public void AddTube(IList<Vector3> path, float radius, int sides, int slot, bool closed = false)
        {
            int n = path.Count;
            if (n < 2) return;
            sides = Mathf.Max(3, sides);
            var rings = new Vector3[n, sides];
            var norms2 = new Vector3[n, sides];
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = path[closed ? (i - 1 + n) % n : Mathf.Max(i - 1, 0)];
                Vector3 next = path[closed ? (i + 1) % n : Mathf.Min(i + 1, n - 1)];
                Vector3 t = (next - prev).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, t);
                if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(Vector3.right, t);
                side.Normalize();
                Vector3 up2 = Vector3.Cross(t, side).normalized;
                for (int k = 0; k < sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = side * Mathf.Cos(a) + up2 * Mathf.Sin(a);
                    rings[i, k] = path[i] + dir * radius;
                    norms2[i, k] = dir;
                }
            }
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int i2 = (i + 1) % n;
                for (int k = 0; k < sides; k++)
                {
                    int k2 = (k + 1) % sides;
                    AddQuadOriented(rings[i, k], rings[i, k2], rings[i2, k2], rings[i2, k],
                                    norms2[i, k], norms2[i, k2], norms2[i2, k2], norms2[i2, k],
                                    new Vector2(k / (float)sides, i), new Vector2((k + 1) / (float)sides, i + 1),
                                    slot, (norms2[i, k] + norms2[i2, k2]).normalized);
                }
            }
        }

        /// <summary>Catmull-Rom で点列をなめらかな曲線にする。</summary>
        public static List<Vector3> Smooth(IList<Vector3> ctrl, int perSegment, bool closed)
        {
            var o = new List<Vector3>();
            int n = ctrl.Count;
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                Vector3 p0 = ctrl[closed ? (i - 1 + n) % n : Mathf.Max(i - 1, 0)];
                Vector3 p1 = ctrl[i];
                Vector3 p2 = ctrl[(i + 1) % n];
                Vector3 p3 = ctrl[closed ? (i + 2) % n : Mathf.Min(i + 2, n - 1)];
                for (int s = 0; s < perSegment; s++)
                {
                    float t = s / (float)perSegment, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            if (!closed) o.Add(ctrl[n - 1]);
            return o;
        }

        // ---------------------------------------------------------------- output

        public Mesh ToMesh(string name, out int[] slots)
        {
            var m = new Mesh
            {
                name = name,
                indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.subMeshCount = subs.Count;
            slots = new int[subs.Count];
            int k = 0;
            foreach (var kv in subs)
            {
                m.SetTriangles(kv.Value, k, false);
                slots[k] = kv.Key;
                k++;
            }
            m.RecalculateBounds();
            // 法線マップ（凹凸の質感）を正しく出すための接線
            if (computeTangents && uvs.Count == verts.Count && verts.Count > 0) m.RecalculateTangents();
            return m;
        }

        public GameObject Build(string name, Transform parent, MaterialLibrary lib,
                                ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = ToMesh(name, out var slots);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = lib.GetMany(slots);
            mr.shadowCastingMode = shadows;
            // どの三角形がどの素材か（芝生の上だけに草を生やすときに使う）
            var info = go.AddComponent<SurfaceSlots>();
            info.slots = slots;
            info.triStart = new int[slots.Length + 1];
            for (int k = 0; k < slots.Length; k++) info.triStart[k + 1] = info.triStart[k] + (int)(mesh.GetIndexCount(k) / 3);
            return go;
        }
    }

    /// <summary>メッシュのサブメッシュ（素材）ごとの三角形の範囲。レイの当たった面の素材を調べるのに使う。</summary>
    public class SurfaceSlots : MonoBehaviour
    {
        public int[] slots;
        public int[] triStart;

        /// <summary>三角形番号から素材（Mat の番号）を返す。分からなければ -1</summary>
        public int SlotOfTriangle(int tri)
        {
            if (slots == null || triStart == null || tri < 0) return -1;
            for (int k = 0; k < slots.Length; k++)
                if (tri >= triStart[k] && tri < triStart[k + 1]) return slots[k];
            return -1;
        }
    }
}
