using System.Collections.Generic;
using UnityEngine;

namespace FutureCity
{
    /// <summary>
    /// 広場のにぎわい：歩く人と座る人（背景の人物）。
    /// 実際の参加者ではないので、参加人数・部屋の人数には数えない。当たり判定もない（あなたの邪魔をしない）。
    /// </summary>
    public static class AmbientPeople
    {
        static readonly Color[] Clothes =
        {
            new Color(0.25f, 0.5f, 0.9f), new Color(0.95f, 0.75f, 0.25f), new Color(0.9f, 0.45f, 0.4f),
            new Color(0.95f, 0.95f, 0.95f), new Color(0.55f, 0.75f, 0.45f), new Color(0.85f, 0.55f, 0.75f),
            new Color(0.35f, 0.7f, 0.8f), new Color(0.98f, 0.6f, 0.35f), new Color(0.6f, 0.55f, 0.85f)
        };

        public static void Spawn(MaterialLibrary lib, MiraiCampus c, MiraiPark.Result park, Transform player, Transform parent)
        {
            var root = new GameObject("AmbientPeople").transform;
            root.SetParent(parent, false);
            var rnd = new System.Random(2127);
            float y = MiraiCampus.DeckTop;

            // ---- 経路（キャンパス中心からの位置）
            var ringCCW = new List<Vector3>();
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                ringCCW.Add(c.W(Mathf.Cos(a) * 14.8f, Mathf.Sin(a) * 14.8f, y));
            }
            var ringCW = new List<Vector3>(ringCCW);
            ringCW.Reverse();
            var routes = new List<List<Vector3>>
            {
                new List<Vector3> { c.W(1.6f, -66f, y), c.W(1.6f, -21f, y), c.W(-1.6f, -21f, y), c.W(-1.6f, -66f, y) },
                new List<Vector3> { c.W(21f, 1.6f, y), c.W(66f, 1.6f, y), c.W(66f, -1.6f, y), c.W(21f, -1.6f, y) },
                new List<Vector3> { c.W(-21f, -1.6f, y), c.W(-66f, -1.6f, y), c.W(-66f, 1.6f, y), c.W(-21f, 1.6f, y) },
                new List<Vector3> { c.W(-1.6f, 21f, y), c.W(-1.6f, 66f, y), c.W(1.6f, 66f, y), c.W(1.6f, 21f, y) },
            };
            var sky = park.skywalk;
            var skyRev = new List<Vector3>(sky);
            skyRev.Reverse();

            int n = 0;
            // ロビーのまわり（ペアで歩く人も）
            for (int k = 0; k < 6; k++) n += Walker(lib, root, ringCCW, k / 6f, rnd, player, n, k % 3 == 0);
            for (int k = 0; k < 4; k++) n += Walker(lib, root, ringCW, k / 4f + 0.1f, rnd, player, n, false);
            // 大通り
            foreach (var r in routes)
                for (int k = 0; k < 3; k++) n += Walker(lib, root, r, k / 3f + (float)rnd.NextDouble() * 0.1f, rnd, player, n, k == 0);
            // 空中回廊（両方向）
            for (int k = 0; k < 5; k++) n += Walker(lib, root, sky, k / 5f, rnd, player, n, k == 2);
            for (int k = 0; k < 4; k++) n += Walker(lib, root, skyRev, k / 4f + 0.12f, rnd, player, n, false);

            // ---- 座っている人（パーゴラのカフェとベンチ）
            for (int i = 0; i < park.cafeSeats.Count; i++)
            {
                if (rnd.NextDouble() < 0.4) continue;
                Sitter(lib, root, park.cafeSeats[i], park.cafeYaw[i], rnd, n++);
            }
            for (int i = 0; i < c.BenchSeats.Count; i++)
            {
                if (rnd.NextDouble() < 0.55) continue;
                Sitter(lib, root, c.BenchSeats[i] + Vector3.up * 0.45f, c.BenchYaw[i], rnd, n++);
            }
        }

        static int RandomAge(System.Random rnd)
        {
            double x = rnd.NextDouble();
            return x < 0.15 ? 6 + rnd.Next(8) : x < 0.35 ? 16 + rnd.Next(14) : x < 0.65 ? 30 + rnd.Next(35) : 65 + rnd.Next(35);
        }

        static float AgeScale(int age) => age < 10 ? 0.66f : age < 14 ? 0.8f : age < 17 ? 0.93f : age >= 85 ? 0.95f : 1f;

        static GameObject Person(MaterialLibrary lib, Transform root, int age, System.Random rnd, int idx)
        {
            var go = PersonFactory.Build(lib, root, "Person" + idx + "_" + age, AgeScale(age), Clothes[rnd.Next(Clothes.Length)], age, false, null);
            SetLayer(go.transform, PlayerWalker.PeopleLayer);
            return go;
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform ch in t) SetLayer(ch, layer);
        }

        /// <summary>歩く人（pair=true なら、子どもと年配の方など2人で並んで歩く）。戻り値は作った人数。</summary>
        static int Walker(MaterialLibrary lib, Transform root, List<Vector3> route, float startFrac, System.Random rnd,
                          Transform player, int idx, bool pair)
        {
            int age = RandomAge(rnd);
            var go = Person(lib, root, age, rnd, idx);
            var w = go.AddComponent<WalkerAI>();
            w.Init(route, startFrac, age >= 75 ? 0.8f : age < 14 ? 1.25f : 1.15f + (float)rnd.NextDouble() * 0.2f, -0.45f, player);
            if (!pair) return 1;

            int age2 = age >= 60 ? 7 + rnd.Next(6) : 70 + rnd.Next(20);   // 世代のちがう2人
            var go2 = Person(lib, root, age2, rnd, idx + 1000);
            var w2 = go2.AddComponent<WalkerAI>();
            w2.Init(route, startFrac, w.speed, 0.45f, player);
            w2.partner = w;
            return 2;
        }

        static void Sitter(MaterialLibrary lib, Transform root, Vector3 seatTop, float yaw, System.Random rnd, int idx)
        {
            int age = RandomAge(rnd);
            if (age < 10) age += 30;
            var go = Person(lib, root, age, rnd, idx + 2000);
            float sc = AgeScale(age);
            go.transform.position = seatTop + Vector3.up * (0.06f - 0.84f * sc);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.GetComponent<PersonAnimator>().sitting = true;
        }
    }

    /// <summary>経路を一定の速さで歩く。地面の高さに合わせ、前にあなたがいると立ち止まる。</summary>
    public class WalkerAI : MonoBehaviour
    {
        public float speed = 1.2f;
        public WalkerAI partner;
        List<Vector3> route;
        int next;
        float lateral;
        Transform player;
        float yaw;

        public void Init(List<Vector3> r, float startFrac, float spd, float lat, Transform pl)
        {
            route = r;
            speed = spd;
            lateral = lat;
            player = pl;
            int start = Mathf.Clamp((int)(startFrac * r.Count) % r.Count, 0, r.Count - 1);
            next = (start + 1) % r.Count;
            transform.position = r[start];
            Vector3 d = r[next] - r[start];
            yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void Update()
        {
            if (route == null || route.Count < 2) return;
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;
            Vector3 target = route[next];
            Vector3 to = target - pos;
            to.y = 0f;
            if (to.magnitude < 0.6f) { next = (next + 1) % route.Count; return; }
            Vector3 dir = to.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir);

            // 前にあなたがいたら待つ（ペアの相方も合わせる）
            float spd = speed;
            if (player != null)
            {
                Vector3 pd = player.position - pos;
                pd.y = 0f;
                if (pd.magnitude < 1.3f && Vector3.Dot(pd.normalized, dir) > 0.3f) spd = 0f;
            }
            if (partner != null && partner.isActiveAndEnabled)
            {
                Vector3 pp = partner.transform.position - pos;
                pp.y = 0f;
                if (Vector3.Dot(pp, dir) > 0.3f) spd *= 1.08f;          // 少し遅れたら追いつく
                else if (Vector3.Dot(pp, dir) < -0.3f) spd *= 0.9f;
            }

            Vector3 goal = pos + dir * spd * dt;
            // 経路の線から少し横にずれて歩く（すれ違い・2人並び）
            Vector3 ideal = ClosestOnSegment(route[(next - 1 + route.Count) % route.Count], target, goal) + side * lateral;
            goal = Vector3.Lerp(goal, new Vector3(ideal.x, goal.y, ideal.z), 1f - Mathf.Exp(-2f * dt));

            // 地面（橋・回廊も）の高さに合わせる
            if (Physics.Raycast(goal + Vector3.up * 1.2f, Vector3.down, out RaycastHit hit, 3f, ~(1 << PlayerWalker.PeopleLayer), QueryTriggerInteraction.Ignore))
                goal.y = Mathf.Lerp(pos.y, hit.point.y, 1f - Mathf.Exp(-12f * dt));
            transform.position = goal;

            float want = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            yaw = Mathf.LerpAngle(yaw, want, 1f - Mathf.Exp(-5f * dt));
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
            return a + ab * t;
        }
    }
}
