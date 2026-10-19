using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// 人物（標準アバター）。肌・髪・服・ズボン・靴の色、年齢に合わせた髪色とめがね、
    /// 歩くと手足が振れる関節つき。色は MaterialPropertyBlock で人ごとに変える（素材は1つ）。
    /// 部品名：Suit（胴）、ArmL/ArmR、LegL/LegR、Head、Halo（任意）
    /// </summary>
    public static class PersonFactory
    {
        static readonly Vector2 T = new Vector2(4f, 4f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static readonly Color[] Skins =
        {
            new Color(0.98f, 0.84f, 0.72f), new Color(0.93f, 0.76f, 0.62f), new Color(0.84f, 0.64f, 0.5f),
            new Color(0.66f, 0.47f, 0.34f), new Color(0.98f, 0.88f, 0.78f)
        };
        static readonly Color[] Pants =
        {
            new Color(0.2f, 0.24f, 0.36f), new Color(0.5f, 0.45f, 0.38f), new Color(0.3f, 0.3f, 0.32f),
            new Color(0.82f, 0.8f, 0.74f), new Color(0.36f, 0.42f, 0.5f)
        };

        static int Hash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char ch in s) h = h * 31 + ch;
                return h & 0x7fffffff;
            }
        }

        /// <summary>布の質感（布目のテクスチャ）を使う部品は、テクスチャが少し暗い分だけ色を明るくする</summary>
        const float FabricBoost = 1.3f;
        static Color Boost(Color c) => new Color(c.r * FabricBoost, c.g * FabricBoost, c.b * FabricBoost, c.a);

        public static void Tint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(mpb);
        }

        /// <summary>服の色を変える（胴と両腕）</summary>
        public static void SetSuitColor(Transform root, Color c)
        {
            if (root == null) return;
            foreach (var n in new[] { "Suit", "ArmL/Sleeve", "ArmR/Sleeve" })
            {
                var t = root.Find(n);
                if (t == null) continue;
                var r = t.GetComponent<Renderer>();
                bool fabric = r != null && r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("FC_Fabric");
                Tint(t.gameObject, fabric ? Boost(c) : c);
            }
        }

        static GameObject Part(MeshBuilder mb, string name, Transform parent, MaterialLibrary lib, Color c, bool fabric = false)
        {
            var go = mb.Build(name, parent, lib, ShadowCastingMode.On);
            Object.Destroy(go.GetComponent<SurfaceSlots>());
            Tint(go, fabric ? Boost(c) : c);
            return go;
        }

        static Transform Pivot(string name, Transform parent, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            return go.transform;
        }

        /// <summary>
        /// 人物を作る。age で髪の色・めがね・姿勢を変える。halo は「デモ人物」の目印（任意）。
        /// </summary>
        public static GameObject Build(MaterialLibrary lib, Transform parent, string name, float scale, Color suit,
                                       int age = 40, bool addCollider = true, Mat? halo = null)
        {
            int h = Hash(name);
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            int m = (int)Mat.Matte;
            // 服（上着・袖・ズボン）は布目の質感。実写テクスチャがないときは従来のつや消し
            bool hasFabric = lib.HasPhotoTextures;
            int f = hasFabric ? (int)Mat.Fabric : m;
            var fineTile = new Vector2(0.25f, 0.25f);

            Color skin = Skins[h % Skins.Length];
            Color pants = Pants[(h / 7) % Pants.Length];
            Color hair = age >= 70 ? new Color(0.9f, 0.9f, 0.88f) : age >= 55 ? new Color(0.62f, 0.6f, 0.58f)
                       : ((h / 13) % 3 == 0 ? new Color(0.35f, 0.22f, 0.12f) : new Color(0.08f, 0.07f, 0.07f));
            Color shoe = (h / 11) % 2 == 0 ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.25f, 0.2f, 0.18f);

            // 胴（上着）
            var torso = new MeshBuilder();
            torso.AddExtrusion(new Vector3(0f, 0.86f, 0f), MeshBuilder.Circle(0.165f, 24), MeshBuilder.Circle(0.2f, 24), 0.46f, 0f, f, f, fineTile, true, false);
            torso.AddSphere(new Vector3(0f, 1.32f, 0f), new Vector3(0.235f, 0.11f, 0.16f), 6, 20, f, 0f);
            torso.AddSphere(new Vector3(0f, 1.38f, 0f), new Vector3(0.055f, 0.06f, 0.055f), 4, 12, f); // 首
            Part(torso, "Suit", root.transform, lib, suit, hasFabric);

            // 腰（ズボン）
            var hip = new MeshBuilder();
            hip.AddSphere(new Vector3(0f, 0.86f, 0f), new Vector3(0.175f, 0.11f, 0.13f), 6, 18, f);
            Part(hip, "Hip", root.transform, lib, pants, hasFabric);

            // 脚（腰を支点に振る）
            for (int sg = -1; sg <= 1; sg += 2)
            {
                var pv = Pivot(sg < 0 ? "LegL" : "LegR", root.transform, new Vector3(sg * 0.09f, 0.84f, 0f));
                var leg = new MeshBuilder();
                leg.AddTube(new[] { Vector3.zero, new Vector3(0f, -0.4f, 0.01f), new Vector3(0f, -0.78f, 0f) }, 0.068f, 12, f);
                leg.AddSphere(new Vector3(0f, -0.4f, 0.01f), Vector3.one * 0.068f, 5, 10, f);
                Part(leg, "Leg", pv, lib, pants, hasFabric);
                var foot = new MeshBuilder();
                foot.AddSphere(new Vector3(0f, -0.8f, 0.045f), new Vector3(0.06f, 0.045f, 0.12f), 5, 12, m);
                Part(foot, "Shoe", pv, lib, shoe);
            }

            // 腕（肩を支点に振る）
            for (int sg = -1; sg <= 1; sg += 2)
            {
                var pv = Pivot(sg < 0 ? "ArmL" : "ArmR", root.transform, new Vector3(sg * 0.24f, 1.33f, 0f));
                var arm = new MeshBuilder();
                arm.AddTube(new[] { Vector3.zero, new Vector3(sg * 0.02f, -0.27f, 0f), new Vector3(sg * 0.02f, -0.5f, 0.03f) }, 0.05f, 10, f);
                arm.AddSphere(Vector3.zero, Vector3.one * 0.055f, 5, 10, f);
                Part(arm, "Sleeve", pv, lib, suit, hasFabric);
                var hand = new MeshBuilder();
                hand.AddSphere(new Vector3(sg * 0.02f, -0.55f, 0.03f), new Vector3(0.042f, 0.055f, 0.035f), 5, 10, m);
                Part(hand, "Hand", pv, lib, skin);
            }

            // 頭（顔・髪・目・めがね）
            var head = Pivot("Head", root.transform, new Vector3(0f, 1.42f, 0f));
            var face = new MeshBuilder();
            face.AddSphere(new Vector3(0f, 0.17f, 0f), new Vector3(0.105f, 0.128f, 0.112f), 9, 20, m);
            face.AddSphere(new Vector3(0f, 0.15f, 0.108f), new Vector3(0.018f, 0.024f, 0.02f), 4, 8, m); // 鼻
            Part(face, "Face", head, lib, skin);
            var hairMb = new MeshBuilder();
            if (age >= 70 && h % 2 == 0)
            {
                hairMb.AddSphere(new Vector3(0f, 0.2f, -0.012f), new Vector3(0.118f, 0.115f, 0.122f), 6, 18, m, -5f, 0.06f, h);
                hairMb.AddSphere(new Vector3(0f, 0.3f, -0.07f), Vector3.one * 0.05f, 5, 10, m);   // お団子
            }
            else if ((h / 3) % 2 == 0)
            {
                hairMb.AddSphere(new Vector3(0f, 0.19f, -0.015f), new Vector3(0.12f, 0.12f, 0.125f), 6, 18, m, -20f, 0.08f, h);   // 長め
            }
            else
            {
                hairMb.AddSphere(new Vector3(0f, 0.21f, -0.01f), new Vector3(0.115f, 0.105f, 0.12f), 6, 18, m, 5f, 0.05f, h);     // 短め
            }
            Part(hairMb, "Hair", head, lib, hair);
            var eyes = new MeshBuilder();
            for (int sg = -1; sg <= 1; sg += 2)
                eyes.AddSphere(new Vector3(sg * 0.038f, 0.185f, 0.1f), new Vector3(0.013f, 0.017f, 0.01f), 4, 8, m);
            Part(eyes, "Eyes", head, lib, new Color(0.12f, 0.1f, 0.1f));
            if (age >= 68 || h % 7 == 0)
            {
                var gl = new MeshBuilder();
                for (int sg = -1; sg <= 1; sg += 2)
                    gl.AddTorus(new Vector3(sg * 0.04f, 0.185f, 0.112f), Quaternion.Euler(90f, 0f, 0f), 0.028f, 0.0045f, 18, 6, m);
                gl.AddTube(new[] { new Vector3(-0.012f, 0.188f, 0.114f), new Vector3(0.012f, 0.188f, 0.114f) }, 0.004f, 6, m);
                Part(gl, "Glasses", head, lib, new Color(0.15f, 0.13f, 0.12f));
            }

            if (halo.HasValue)
            {
                var hb = new MeshBuilder();
                hb.AddTorus(Vector3.zero, Quaternion.identity, 0.2f, 0.012f, 40, 6, (int)halo.Value);
                var hg = hb.Build("Halo", root.transform, lib, ShadowCastingMode.Off);
                hg.transform.localPosition = new Vector3(0f, 1.96f, 0f);
            }

            if (addCollider)
            {
                var col = root.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.9f, 0f);
                col.height = 1.8f;
                col.radius = 0.3f;
            }

            var anim = root.AddComponent<PersonAnimator>();
            anim.age = age;
            root.transform.localScale = Vector3.one * scale;
            return root;
        }
    }

    /// <summary>
    /// 人物の動き：移動の速さから手足を振る。止まっているときは軽い呼吸。座っているときは脚を曲げる。
    /// 位置の変化から速さを求めるので、プレイヤー・デモ人物・歩く人のどれにもそのまま使える。
    /// </summary>
    public class PersonAnimator : MonoBehaviour
    {
        public int age = 40;
        public bool sitting;

        Transform legL, legR, armL, armR, head;
        Vector3 lastPos;
        float speed, phase, seed;

        void Start()
        {
            legL = transform.Find("LegL");
            legR = transform.Find("LegR");
            armL = transform.Find("ArmL");
            armR = transform.Find("ArmR");
            head = transform.Find("Head");
            lastPos = transform.position;
            seed = Random.value * 10f;
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 d = transform.position - lastPos;
            d.y = 0f;
            lastPos = transform.position;
            float sc = Mathf.Max(0.3f, transform.lossyScale.y);
            float raw = d.magnitude / dt / sc;
            speed = Mathf.Lerp(speed, raw, 1f - Mathf.Exp(-10f * dt));

            float amt = Mathf.Clamp01(speed / 1.3f);
            float stride = age >= 75 ? 22f : 32f;
            phase += dt * (3.2f + speed * 2.6f) * (amt > 0.05f ? 1f : 0f);
            float swing = Mathf.Sin(phase) * stride * amt;

            if (sitting)
            {
                Set(legL, -85f, 0f);
                Set(legR, -85f, 0f);
                Set(armL, -25f, 4f);
                Set(armR, -25f, -4f);
            }
            else
            {
                Set(legL, swing, 0f);
                Set(legR, -swing, 0f);
                float idle = Mathf.Sin(Time.time * 1.4f + seed) * 2f * (1f - amt);
                Set(armL, -swing * 0.8f + idle, 4f);
                Set(armR, swing * 0.8f - idle, -4f);
            }
            if (head != null)
            {
                float nod = age >= 80 ? 6f : 0f;   // ご年配の方は少しうつむき加減
                head.localRotation = Quaternion.Euler(nod + Mathf.Sin(Time.time * 0.7f + seed) * 2f, Mathf.Sin(Time.time * 0.3f + seed) * 6f, 0f);
            }
        }

        static void Set(Transform t, float pitch, float roll)
        {
            if (t != null) t.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
