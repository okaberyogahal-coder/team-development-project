using System.Collections.Generic;
using UnityEngine;

namespace FutureCity
{
    /// <summary>閉じた折れ線経路。距離 s から位置と接線を求める。</summary>
    public class PathLine
    {
        public readonly List<Vector3> Points;
        readonly float[] cum;
        public float Length { get; }

        public PathLine(List<Vector3> pts)
        {
            Points = pts;
            int n = pts.Count;
            cum = new float[n + 1];
            for (int i = 0; i < n; i++) cum[i + 1] = cum[i] + Vector3.Distance(pts[i], pts[(i + 1) % n]);
            Length = cum[n];
        }

        public Vector3 Position(float s)
        {
            s = Mathf.Repeat(s, Length);
            int lo = 0, hi = Points.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (cum[mid] <= s) lo = mid; else hi = mid - 1;
            }
            int i = lo, j = (lo + 1) % Points.Count;
            float seg = cum[i + 1] - cum[i];
            float t = seg > 1e-5f ? (s - cum[i]) / seg : 0f;
            return Vector3.Lerp(Points[i], Points[j], t);
        }

        public void Eval(float s, out Vector3 pos, out Vector3 tangent)
        {
            pos = Position(s);
            tangent = (Position(s + 2f) - Position(s - 2f)).normalized;
        }

        /// <summary>角丸長方形（反時計回り）。</summary>
        public static List<Vector3> RoundedRect(float x0, float z0, float x1, float z1, float r, float y, float step)
        {
            var pts = new List<Vector3>();
            Line(pts, new Vector3(x0 + r, y, z0), new Vector3(x1 - r, y, z0), step);
            Arc(pts, new Vector3(x1 - r, y, z0 + r), r, -90f, 0f);
            Line(pts, new Vector3(x1, y, z0 + r), new Vector3(x1, y, z1 - r), step);
            Arc(pts, new Vector3(x1 - r, y, z1 - r), r, 0f, 90f);
            Line(pts, new Vector3(x1 - r, y, z1), new Vector3(x0 + r, y, z1), step);
            Arc(pts, new Vector3(x0 + r, y, z1 - r), r, 90f, 180f);
            Line(pts, new Vector3(x0, y, z1 - r), new Vector3(x0, y, z0 + r), step);
            Arc(pts, new Vector3(x0 + r, y, z0 + r), r, 180f, 270f);
            return pts;
        }

        static void Line(List<Vector3> pts, Vector3 a, Vector3 b, float step)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / step));
            for (int i = 0; i < n; i++) pts.Add(Vector3.Lerp(a, b, i / (float)n));
        }

        static void Arc(List<Vector3> pts, Vector3 c, float r, float a0, float a1)
        {
            const int n = 12;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
                pts.Add(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
            }
        }
    }
}
