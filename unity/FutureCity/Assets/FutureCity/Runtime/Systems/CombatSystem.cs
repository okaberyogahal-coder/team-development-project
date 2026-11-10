using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// 戦闘モード（F キーで切りかえ）。
    ///  - 左クリック：腕の端末から光のビームを撃つ（画面中央の照準へ）
    ///  - 右クリック（押している間）：ねらう（肩ごしの視点に寄り、少しズーム）
    ///  - E：パンチ（目の前の人）
    ///  - Enter：話しかける・入室する（戦闘モード中も使える）
    ///  - 戦闘モード中はクリックで歩く操作は止める（左クリックは撃つため）。W A S D で歩ける
    /// 広場の人は「パーソナルシールド」に守られている設定：当たるとシールドの輪が光り、よろける・倒れる・起き上がって元に戻る。
    /// 座っている人は身をすくめるだけ。血などの表現はない。
    /// 効果音はすべてその場で合成する（音のファイルは不要）。
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class CombatSystem : MonoBehaviour
    {
        public static bool Active { get; private set; }
        public static bool Aiming { get; private set; }
        public static CombatSystem Instance { get; private set; }

        public PlayerWalker player;
        public Camera cam;

        [Header("ビーム")]
        public float fireInterval = 0.18f;
        public float range = 150f;
        public Color beamColor = new Color(0.45f, 0.9f, 1f);
        [Header("パンチ")]
        public float punchRange = 1.6f;
        public float punchDuration = 0.32f;

        MaterialLibrary lib;
        Transform avatar, armR, armL;
        GameObject wrist;
        AudioSource sfx;
        Material fxMat;
        float cooldown, punchT = -1f, shotT = -1f, flashT = -1f;
        bool punchHitDone;
        float baseFov = -1f;
        int shots, hits;

        public int Shots => shots;
        public int Hits => hits;

        public void Init(PlayerWalker p, Camera c, MaterialLibrary library)
        {
            Instance = this;
            player = p;
            cam = c;
            lib = library;
            avatar = p != null ? p.body : null;
            if (avatar != null)
            {
                armR = avatar.Find("ArmR");
                armL = avatar.Find("ArmL");
            }
            fxMat = CombatFx.CreateMaterial(lib);
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            sfx.volume = 0.8f;
            SynthSounds.Build();
            BuildWrist();
            Active = false;
            Aiming = false;
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; Active = false; Aiming = false; }
            CombatFx.Release(fxMat);
        }

        /// <summary>腕の端末（戦闘モードのときだけ見える、手首の光る輪）</summary>
        void BuildWrist()
        {
            if (armR == null || lib == null) return;
            var mb = new MeshBuilder();
            mb.AddTorus(Vector3.zero, Quaternion.identity, 0.052f, 0.013f, 24, 8, (int)Mat.NeonCyan);
            wrist = mb.Build("CombatWrist", armR, lib, ShadowCastingMode.Off);
            wrist.transform.localPosition = new Vector3(0f, -0.47f, 0.022f);
            wrist.SetActive(false);
        }

        public void SetActive(bool on)
        {
            if (Active == on) return;
            Active = on;
            Aiming = false;
            if (wrist != null) wrist.SetActive(on);
            Play(on ? SynthSounds.ModeOn : SynthSounds.ModeOff, 0.35f);
            if (on)
            {
                PlayerWalker.LookWanted = true;      // 照準で狙えるよう、マウスで見回すモードに
                if (player != null) player.StopAuto();
                MiraiWorld.Notify("戦闘モード：左クリックでビーム、右クリックでねらう、E でパンチ、Enter で話す。F でもどります。");
            }
            else MiraiWorld.Notify("ふだんのモードにもどりました。");
        }

        public void Toggle() => SetActive(!Active);

        void Update()
        {
            float dt = Time.deltaTime;
            cooldown -= dt;
            bool blocked = MiraiWorld.UIOpen || MiraiWorld.IntroShowing;
            if (blocked)
            {
                Aiming = false;
                return;
            }
            if (InputBridge.Down(FKey.F)) Toggle();
            if (!Active) { Aiming = false; return; }

            Aiming = InputBridge.RightMouse;
            bool clickOk = (PlayerWalker.FreeLook || !MiraiWorld.PointerOverUI) && (player == null || !player.ClickConsumed);
            if (InputBridge.LeftMouseDown && clickOk && cooldown <= 0f) Fire();
            if (InputBridge.Down(FKey.E) && punchT < 0f) Punch();
        }

        /// <summary>照準（見回しモードなら画面中央、カーソルを出しているならマウスの位置）を通るレイ</summary>
        public Ray AimRay()
        {
            Vector2 aim = PlayerWalker.FreeLook || cam == null ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) : InputBridge.MousePosition;
            if (cam == null) return new Ray(transform.position + Vector3.up * 1.5f, transform.forward);
            return cam.ScreenPointToRay(new Vector3(aim.x, aim.y, 0f));
        }

        public Vector3 HandPosition
        {
            get
            {
                if (armR != null) return armR.TransformPoint(new Vector3(0f, -0.56f, 0.04f));
                return transform.position + Vector3.up * 1.2f + transform.forward * 0.4f;
            }
        }

        /// <summary>ビームを撃つ（照準の先へ）。当たった人はよろける・倒れる</summary>
        public void Fire() => FireRay(AimRay());

        public void FireRay(Ray ray)
        {
            cooldown = fireInterval;
            shots++;
            shotT = 0f;
            flashT = 0f;
            Vector3 end = ray.origin + ray.direction * range;
            Vector3 normal = -ray.direction;
            PersonAnimator victim = null;
            var all = Physics.RaycastAll(ray.origin, ray.direction, range, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(all, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in all)
            {
                if (h.collider == null) continue;
                if (player != null && h.collider.transform.IsChildOf(player.transform)) continue;
                var pa = h.collider.GetComponentInParent<PersonAnimator>();
                if (pa == null && h.collider.isTrigger) continue;     // 人以外のトリガーは素通り
                end = h.point;
                normal = h.normal;
                victim = pa;
                break;
            }
            Vector3 hand = HandPosition;
            CombatFx.Beam(fxMat, hand, end, beamColor);
            CombatFx.Flash(fxMat, hand, beamColor);
            Play(SynthSounds.Pew, 0.55f, Random.Range(0.94f, 1.06f));
            if (victim != null)
            {
                hits++;
                Vector3 dir = end - hand; dir.y = 0f;
                HitReaction.Hit(victim, dir.sqrMagnitude > 1e-4f ? dir.normalized : transform.forward, end, false, fxMat);
                PlayAt(SynthSounds.ShieldHit, end, 0.9f);
            }
            else
            {
                CombatFx.Sparks(fxMat, end, normal, 14, new Color(1f, 0.82f, 0.45f));
                PlayAt(SynthSounds.Impact, end, 0.6f);
            }
        }

        /// <summary>パンチ（目の前・1.6m 以内の人）</summary>
        public void Punch()
        {
            punchT = 0f;
            punchHitDone = false;
            Play(SynthSounds.Whoosh, 0.6f, Random.Range(0.95f, 1.05f));
        }

        void PunchHit()
        {
            punchHitDone = true;
            Transform body = avatar != null ? avatar : transform;
            Vector3 fwd = body.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 origin = transform.position;
            PersonAnimator best = null;
            float bestD = punchRange;
            foreach (var pa in PersonAnimator.All)
            {
                if (pa == null || !pa.isActiveAndEnabled) continue;
                if (player != null && pa.transform.IsChildOf(player.transform)) continue;
                Vector3 d = pa.transform.position - origin; d.y = 0f;
                float dist = d.magnitude;
                if (dist > bestD || dist < 0.05f) continue;
                if (Vector3.Dot(d / dist, fwd) < 0.45f) continue;      // 正面からおよそ ±63° 以内
                best = pa;
                bestD = dist;
            }
            Vector3 fist = HandPosition;
            if (best != null)
            {
                hits++;
                Vector3 contact = best.transform.position + Vector3.up * (1.15f * best.transform.lossyScale.y) - fwd * 0.25f;
                HitReaction.Hit(best, fwd, contact, true, fxMat);
                PlayAt(SynthSounds.Punch, contact, 0.9f);
                PlayAt(SynthSounds.ShieldHit, contact, 0.6f);
            }
            else CombatFx.Sparks(fxMat, fist + fwd * 0.2f, -fwd, 5, new Color(0.7f, 0.95f, 1f));
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (punchT >= 0f)
            {
                punchT += dt;
                if (!punchHitDone && punchT >= punchDuration * 0.45f) PunchHit();
                if (punchT > punchDuration) punchT = -1f;
            }
            if (shotT >= 0f) { shotT += dt; if (shotT > 0.35f) shotT = -1f; }
            if (flashT >= 0f) { flashT += dt; if (flashT > 0.1f) flashT = -1f; }

            // 腕の姿勢（PersonAnimator のあとで上書き）
            if (Active && armR != null)
            {
                float camPitch = cam != null ? NormalizeAngle(cam.transform.eulerAngles.x) : 0f;
                if (punchT >= 0f)
                {
                    float p = Mathf.Clamp01(punchT / punchDuration);
                    float ext = p < 0.45f ? Mathf.SmoothStep(0f, 1f, p / 0.45f) : Mathf.SmoothStep(1f, 0f, (p - 0.45f) / 0.55f);
                    armR.localRotation = Quaternion.Euler(Mathf.Lerp(-20f, -92f, ext), 0f, Mathf.Lerp(-4f, 6f, ext));
                    if (armL != null) armL.localRotation = Quaternion.Euler(Mathf.Lerp(-10f, 25f, ext), 0f, 6f);
                }
                else if (Aiming || shotT >= 0f)
                {
                    float k = Aiming ? 1f : Mathf.Clamp01(1f - shotT / 0.35f);
                    float pitch = Mathf.Clamp(-90f + camPitch, -130f, -40f);
                    var aimRot = Quaternion.Euler(pitch, 0f, -3f);
                    armR.localRotation = Quaternion.Slerp(armR.localRotation, aimRot, k);
                }
            }
        }

        static float NormalizeAngle(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            if (a < -180f) a += 360f;
            return a;
        }

        void Play(AudioClip c, float vol, float pitch = 1f)
        {
            if (c == null || sfx == null) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(c, vol);
        }

        static void PlayAt(AudioClip c, Vector3 p, float vol)
        {
            if (c != null) AudioSource.PlayClipAtPoint(c, p, vol);
        }

        // ------------------------------------------------------------------ 画面：照準と操作の案内
        void OnGUI()
        {
            if (!Active || MiraiWorld.UIOpen || MiraiWorld.IntroShowing) return;
            float s = Mathf.Max(0.6f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float W = Screen.width / s, H = Screen.height / s;
            Vector2 c = new Vector2(W * 0.5f, H * 0.5f);
            if (!PlayerWalker.FreeLook)
            {
                Vector2 mp = InputBridge.MousePosition;
                c = new Vector2(mp.x / s, (Screen.height - mp.y) / s);
            }
            // 照準：4 本の短い線と中央の点（ねらう間はせまく、撃った直後は少し広がる）
            float gap = (Aiming ? 7f : 12f) + (shotT >= 0f ? 8f * (1f - shotT / 0.35f) : 0f);
            float len = Aiming ? 10f : 12f;
            var old = GUI.color;
            Color line = new Color(0.55f, 0.95f, 1f, 0.95f);
            Color shadow = new Color(0f, 0.05f, 0.1f, 0.55f);
            for (int pass = 0; pass < 2; pass++)
            {
                GUI.color = pass == 0 ? shadow : line;
                float o = pass == 0 ? 1.5f : 0f, w = pass == 0 ? 5f : 2.5f;
                GUI.DrawTexture(new Rect(c.x - gap - len - o, c.y - w * 0.5f, len + o * 2f, w), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(c.x + gap - o, c.y - w * 0.5f, len + o * 2f, w), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(c.x - w * 0.5f, c.y - gap - len - o, w, len + o * 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(c.x - w * 0.5f, c.y + gap - o, w, len + o * 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(c.x - w * 0.5f, c.y - w * 0.5f, w, w), Texture2D.whiteTexture);
            }
            // 上の案内（大きな文字・おだやかな色）
            string hint = "<b>戦闘モード</b>　　左クリック：ビーム　／　右クリック：ねらう　／　E：パンチ　／　Enter：話す　／　F：もどる";
            var st = new GUIStyle(GUI.skin.box) { fontSize = 22, richText = true, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            st.normal.textColor = Color.white;
            float hw = Mathf.Min(1180f, W - 40f);
            GUI.color = new Color(0.08f, 0.22f, 0.32f, 0.82f);
            GUI.DrawTexture(new Rect((W - hw) * 0.5f, 18f, hw, 50f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect((W - hw) * 0.5f, 18f, hw, 50f), hint, st);
            GUI.color = old;
        }
    }

    // =====================================================================================================
    /// <summary>
    /// 人に当たったときの動き（よろける → 倒れる → 起き上がる → 元の場所・向きにもどる）。座っている人は身をすくめるだけ。
    /// 歩く人（WalkerAI）と住民（Citizen）の動きは、反応のあいだだけ止める。
    /// </summary>
    public class HitReaction : MonoBehaviour
    {
        enum State { None, Stagger, Fall, Down, GetUp, Flinch }

        State state = State.None;
        float t, downTime;
        Vector3 basePos, knock;
        Quaternion baseRot;
        Vector3 axis;
        float lastHit = -10f;
        bool returning;
        readonly List<Behaviour> paused = new List<Behaviour>();
        PersonAnimator anim;
        Material fx;

        public bool Busy => state != State.None;
        public bool IsDown => state == State.Fall || state == State.Down;

        /// <summary>person に当たった（dir：押される向き、point：当たった所、strong：パンチなど強い当たり）</summary>
        public static void Hit(PersonAnimator person, Vector3 dir, Vector3 point, bool strong, Material fxMat)
        {
            if (person == null) return;
            var hr = person.GetComponent<HitReaction>();
            if (hr == null) hr = person.gameObject.AddComponent<HitReaction>();
            hr.anim = person;
            hr.fx = fxMat;
            hr.Apply(dir, point, strong);
        }

        void Apply(Vector3 dir, Vector3 point, bool strong)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = -transform.forward;
            dir.Normalize();
            float s = Mathf.Max(0.5f, transform.lossyScale.y);
            // シールドの輪（当たった所に、こちらを向いて）
            CombatFx.Shield(fx, point, -dir, 0.55f * s);
            CombatFx.Sparks(fx, point, -dir, 8, new Color(0.6f, 0.95f, 1f));

            bool seated = anim != null && anim.sitting;
            float now = Time.time;
            bool quickAgain = now - lastHit < 1.6f;
            lastHit = now;

            if (returning)
            {
                // 元の場所へもどる途中にまた当たった：もどる動きをやめ、元の場所・向き（basePos/baseRot）はそのまま使う
                StopAllCoroutines();
                returning = false;
            }
            else if (state == State.None) Capture();
            if (seated)
            {
                Begin(State.Flinch, dir);
                return;
            }
            switch (state)
            {
                case State.None:
                case State.Flinch:
                    Begin(strong ? State.Fall : State.Stagger, dir);
                    break;
                case State.Stagger:
                    Begin(State.Fall, dir);        // よろけている間にもう一度 → 倒れる
                    break;
                case State.Fall:
                case State.Down:
                    downTime = Mathf.Min(downTime + 0.6f, 3.6f);
                    break;
                case State.GetUp:
                    Begin(State.Down, axisDirToKnock());
                    break;
            }
            if (quickAgain && state == State.Stagger) Begin(State.Fall, dir);
        }

        Vector3 axisDirToKnock() => knock.sqrMagnitude > 1e-6f ? knock.normalized : -transform.forward;

        void Capture()
        {
            basePos = transform.position;
            baseRot = transform.rotation;
            paused.Clear();
            foreach (var b in GetComponents<Behaviour>())
            {
                if (b == null || b == this || b is PersonAnimator) continue;
                if ((b is WalkerAI || b is Citizen) && b.enabled) { b.enabled = false; paused.Add(b); }
            }
        }

        void Begin(State st, Vector3 dir)
        {
            state = st;
            t = 0f;
            if (dir.sqrMagnitude > 1e-6f)
            {
                knock = dir.normalized;
                axis = Vector3.Cross(Vector3.up, knock);
            }
            if (st == State.Fall) downTime = 2.2f;
            if (st == State.Down && downTime <= 0f) downTime = 1.6f;
        }

        void LateUpdate()
        {
            if (state == State.None) return;
            float dt = Time.deltaTime;
            t += dt;
            float s = Mathf.Max(0.5f, transform.lossyScale.y);
            float tilt = 0f, slide = 0f, lift = 0f;
            switch (state)
            {
                case State.Flinch:
                {
                    float p = Mathf.Clamp01(t / 0.45f);
                    tilt = 11f * Mathf.Sin(p * Mathf.PI) * (1f - 0.3f * p);
                    tilt += Mathf.Sin(t * 60f) * 2.5f * (1f - p);
                    if (p >= 1f) Finish();
                    break;
                }
                case State.Stagger:
                {
                    float p = Mathf.Clamp01(t / 0.7f);
                    float k = p < 0.3f ? Mathf.SmoothStep(0f, 1f, p / 0.3f) : Mathf.SmoothStep(1f, 0f, (p - 0.3f) / 0.7f);
                    tilt = 20f * k;
                    slide = 0.35f * s * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, p / 0.35f));
                    if (p >= 1f) Settle();
                    break;
                }
                case State.Fall:
                {
                    float p = Mathf.Clamp01(t / 0.6f);
                    float k = p * p * (3f - 2f * p);
                    tilt = 88f * k + Mathf.Sin(Mathf.Clamp01((t - 0.6f) / 0.25f) * Mathf.PI) * 4f;
                    slide = 0.55f * s * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, p * 1.4f));
                    lift = 0.10f * s * k;
                    if (t >= 0.6f) { state = State.Down; t = 0f; }
                    break;
                }
                case State.Down:
                {
                    tilt = 88f + Mathf.Sin(t * 1.3f) * 1.2f;
                    slide = 0.55f * s;
                    lift = 0.10f * s;
                    if (t >= downTime) { state = State.GetUp; t = 0f; }
                    break;
                }
                case State.GetUp:
                {
                    float p = Mathf.Clamp01(t / 1.1f);
                    float k = 1f - Mathf.SmoothStep(0f, 1f, p);
                    tilt = 88f * k - Mathf.Sin(p * Mathf.PI) * 5f * (1f - p);
                    slide = 0.55f * s * (k > 0f ? 1f : 0f);
                    lift = 0.10f * s * k;
                    if (p >= 1f) Settle();
                    break;
                }
            }
            if (state == State.None) return;
            transform.rotation = Quaternion.AngleAxis(tilt, axis) * baseRot;
            transform.position = basePos + knock * slide + Vector3.up * lift;
        }

        /// <summary>立ち上がったあと：歩く人はその場から歩きだす。住民は元の場所へもどる</summary>
        void Settle()
        {
            bool walker = false;
            foreach (var b in paused) if (b is WalkerAI) walker = true;
            if (walker)
            {
                Vector3 p = transform.position;
                p.y = basePos.y;
                transform.position = p;
                transform.rotation = baseRot;
                Finish();
                return;
            }
            // 住民：0.6 秒かけて元の位置へ
            state = State.None;
            returning = true;
            StartCoroutine(Return());
        }

        System.Collections.IEnumerator Return()
        {
            Vector3 from = transform.position;
            Quaternion fromRot = transform.rotation;
            float u = 0f;
            while (u < 1f)
            {
                u = Mathf.Min(1f, u + Time.deltaTime / 0.6f);
                float k = Mathf.SmoothStep(0f, 1f, u);
                transform.position = Vector3.Lerp(from, basePos, k);
                transform.rotation = Quaternion.Slerp(fromRot, baseRot, k);
                yield return null;
            }
            returning = false;
            Finish();
        }

        void Finish()
        {
            if (state == State.Flinch || state == State.Stagger || state == State.GetUp)
            {
                transform.rotation = baseRot;
                if (state == State.Flinch) transform.position = basePos;
            }
            state = State.None;
            foreach (var b in paused) if (b != null) b.enabled = true;
            paused.Clear();
        }
    }

    // =====================================================================================================
    /// <summary>光の効果（ビーム・火花・シールドの輪・手もとの光）。どれも数十分の 1 秒〜0.5 秒で消える</summary>
    public static class CombatFx
    {
        public static Material CreateMaterial(MaterialLibrary lib)
        {
            var sh = Shader.Find("MiraiToon/Effect");
            if (sh != null && sh.isSupported)
            {
                var m = new Material(sh) { name = "MT_Effect", renderQueue = (int)RenderQueue.Transparent + 10 };
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Intensity", 2.2f);
                return m;
            }
            var fallback = lib != null ? lib.Get(Mat.HoloCyan) : null;
            // The combat system owns its copy; never destroy the world's shared material.
            return fallback != null ? new Material(fallback) : null;
        }

        public static void Release(UnityEngine.Object resource)
        {
            if(resource == null)return;
            if(Application.isPlaying)UnityEngine.Object.Destroy(resource);
            else UnityEngine.Object.DestroyImmediate(resource);
        }

        static GameObject Make(string name, Material mat, out Mesh mesh)
        {
            var go = new GameObject(name);
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            go.AddComponent<CombatMeshOwner>().mesh = mesh;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        public static void Beam(Material mat, Vector3 a, Vector3 b, Color c)
        {
            if (mat == null) return;
            Mesh m;
            var go = Make("FxBeam", mat, out m);
            var fx = go.AddComponent<FxBeam>();
            fx.Init(m, a, b, c);
        }

        public static void Flash(Material mat, Vector3 p, Color c)
        {
            if (mat == null) return;
            Mesh m;
            var go = Make("FxFlash", mat, out m);
            var fx = go.AddComponent<FxSparks>();
            fx.Init(m, p, Vector3.up, 8, new Color(0.8f, 1f, 1f), 0.12f, 1.8f, 0.6f);
        }

        public static void Sparks(Material mat, Vector3 p, Vector3 n, int count, Color c)
        {
            if (mat == null) return;
            Mesh m;
            var go = Make("FxSparks", mat, out m);
            var fx = go.AddComponent<FxSparks>();
            fx.Init(m, p, n, count, c, 0.38f, 5.5f, 0.0f);
        }

        public static void Shield(Material mat, Vector3 p, Vector3 facing, float radius)
        {
            if (mat == null) return;
            Mesh m;
            var go = Make("FxShield", mat, out m);
            var fx = go.AddComponent<FxRing>();
            fx.Init(m, p, facing, radius, new Color(0.45f, 0.92f, 1f));
        }

        /// <summary>a→b の帯を、カメラの方を向けて 4 頂点で作る（v=0〜1 が帯の幅）</summary>
        public static void Strip(List<Vector3> v, List<Color> col, List<Vector2> uv, List<int> tri, Vector3 a, Vector3 b, float wa, float wb, Color ca, Color cb, Vector3 camPos)
        {
            Vector3 d = b - a;
            Vector3 mid = (a + b) * 0.5f;
            Vector3 side = Vector3.Cross(d, camPos - mid);
            if (side.sqrMagnitude < 1e-10f) side = Vector3.Cross(d, Vector3.up);
            side = side.normalized;
            int i = v.Count;
            v.Add(a - side * wa); v.Add(a + side * wa); v.Add(b + side * wb); v.Add(b - side * wb);
            col.Add(ca); col.Add(ca); col.Add(cb); col.Add(cb);
            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(0f, 1f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(1f, 0f));
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }

        public static Vector3 CamPos => Camera.main != null ? Camera.main.transform.position : Vector3.zero;
    }

    // Each effect owns its generated mesh; materials are shared across live effects.
    public sealed class CombatMeshOwner : MonoBehaviour
    {
        public Mesh mesh;
        void OnDestroy(){CombatFx.Release(mesh);}
    }

    /// <summary>ビーム：白い芯＋水色の光。0.14 秒で細く・薄くなって消える</summary>
    public class FxBeam : MonoBehaviour
    {
        Mesh mesh;
        Vector3 a, b;
        Color c;
        float t;
        const float Life = 0.14f;
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Color> col = new List<Color>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> tri = new List<int>();

        public void Init(Mesh m, Vector3 from, Vector3 to, Color color)
        {
            mesh = m; a = from; b = to; c = color;
            Rebuild();
        }

        void LateUpdate()
        {
            t += Time.deltaTime;
            if (t >= Life) { Destroy(gameObject); return; }
            Rebuild();
        }

        void Rebuild()
        {
            float k = 1f - t / Life;
            v.Clear(); col.Clear(); uv.Clear(); tri.Clear();
            Vector3 cp = CombatFx.CamPos;
            // 外の光（幅広）→ 芯（細い白）
            CombatFx.Strip(v, col, uv, tri, a, b, 0.11f * k + 0.015f, 0.08f * k + 0.015f, new Color(c.r, c.g, c.b, 1.0f * k), new Color(c.r, c.g, c.b, 0.7f * k), cp);
            CombatFx.Strip(v, col, uv, tri, a, b, 0.026f * k + 0.006f, 0.018f * k + 0.004f, new Color(1f, 1f, 1f, 1.8f * k), new Color(1f, 1f, 1f, 1.4f * k), cp);
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetColors(col);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
        }
    }

    /// <summary>火花：短い光の筋がはじけて落ちる</summary>
    public class FxSparks : MonoBehaviour
    {
        Mesh mesh;
        Vector3[] p, vel;
        float[] life, maxLife;
        Color c;
        float t, duration, gravity = 9f;
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Color> col = new List<Color>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> tri = new List<int>();

        public void Init(Mesh m, Vector3 pos, Vector3 n, int count, Color color, float dur, float speed, float spread)
        {
            mesh = m; c = color; duration = dur;
            p = new Vector3[count]; vel = new Vector3[count]; life = new float[count]; maxLife = new float[count];
            if (n.sqrMagnitude < 1e-6f) n = Vector3.up;
            n.Normalize();
            for (int i = 0; i < count; i++)
            {
                Vector3 r = Random.onUnitSphere;
                if (spread <= 0f && Vector3.Dot(r, n) < 0f) r = -r;          // 面の外側の半球へ
                Vector3 d = (spread > 0f ? r : (n * 0.8f + r).normalized);
                p[i] = pos;
                vel[i] = d * speed * Random.Range(0.45f, 1f);
                maxLife[i] = dur * Random.Range(0.55f, 1f);
                life[i] = 0f;
            }
            if (spread > 0f) gravity = 0f;
            Rebuild();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            t += dt;
            if (t >= duration) { Destroy(gameObject); return; }
            for (int i = 0; i < p.Length; i++)
            {
                life[i] += dt;
                vel[i] += Vector3.down * gravity * dt;
                vel[i] *= Mathf.Exp(-2.5f * dt);
                p[i] += vel[i] * dt;
            }
            Rebuild();
        }

        void Rebuild()
        {
            v.Clear(); col.Clear(); uv.Clear(); tri.Clear();
            Vector3 cp = CombatFx.CamPos;
            for (int i = 0; i < p.Length; i++)
            {
                float k = 1f - Mathf.Clamp01(life[i] / maxLife[i]);
                if (k <= 0f) continue;
                Vector3 tail = p[i] - vel[i] * 0.035f;
                Color head = new Color(Mathf.Lerp(1f, c.r, 1f - k), Mathf.Lerp(1f, c.g, 1f - k), Mathf.Lerp(1f, c.b, 1f - k), 1.8f * k);
                Color tailC = new Color(c.r, c.g, c.b, 0.4f * k);
                CombatFx.Strip(v, col, uv, tri, tail, p[i], 0.008f, 0.014f * k + 0.004f, tailC, head, cp);
            }
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetColors(col);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
        }
    }

    /// <summary>シールドの輪：当たった所で、こちらを向いた光の輪が広がって消える（六角形の細い模様つき）</summary>
    public class FxRing : MonoBehaviour
    {
        Mesh mesh;
        Vector3 center, nrm, u, w;
        float radius, t;
        Color c;
        const float Life = 0.45f;
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Color> col = new List<Color>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> tri = new List<int>();

        public void Init(Mesh m, Vector3 p, Vector3 facing, float r, Color color)
        {
            mesh = m; center = p; radius = r; c = color;
            nrm = facing.sqrMagnitude > 1e-6f ? facing.normalized : Vector3.forward;
            u = Vector3.Cross(nrm, Vector3.up);
            if (u.sqrMagnitude < 1e-6f) u = Vector3.Cross(nrm, Vector3.right);
            u.Normalize();
            w = Vector3.Cross(nrm, u).normalized;
            Rebuild();
        }

        void LateUpdate()
        {
            t += Time.deltaTime;
            if (t >= Life) { Destroy(gameObject); return; }
            Rebuild();
        }

        void Ring(float r, float width, float alpha, int seg, float sides)
        {
            int start = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                // sides>0 なら多角形（六角形）に寄せる
                float rr = r;
                if (sides > 0f)
                {
                    float sector = Mathf.PI * 2f / sides;
                    float local = Mathf.Repeat(a, sector) - sector * 0.5f;
                    rr = r * Mathf.Cos(sector * 0.5f) / Mathf.Cos(local);
                }
                Vector3 dir = u * Mathf.Cos(a) + w * Mathf.Sin(a);
                v.Add(center + dir * (rr - width * 0.5f));
                v.Add(center + dir * (rr + width * 0.5f));
                Color cc = new Color(c.r, c.g, c.b, alpha);
                col.Add(cc); col.Add(cc);
                uv.Add(new Vector2(i / (float)seg, 0f)); uv.Add(new Vector2(i / (float)seg, 1f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = start + i * 2;
                tri.Add(a0); tri.Add(a0 + 1); tri.Add(a0 + 3);
                tri.Add(a0); tri.Add(a0 + 3); tri.Add(a0 + 2);
            }
        }

        void Rebuild()
        {
            float p = t / Life;
            float grow = 1f - (1f - p) * (1f - p);
            float fade = 1f - p;
            v.Clear(); col.Clear(); uv.Clear(); tri.Clear();
            float r = radius * (0.45f + 0.75f * grow);
            Ring(r, 0.07f * radius + 0.02f, 1.6f * fade, 48, 0f);
            Ring(r * 0.72f, 0.035f * radius + 0.01f, 0.9f * fade, 36, 6f);
            Ring(r * 0.42f, 0.03f * radius + 0.01f, 0.6f * fade, 24, 6f);
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetColors(col);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
        }
    }

    // =====================================================================================================
    /// <summary>効果音をその場で合成する（44.1kHz・モノラル）。音のファイルや外部アセットは使わない</summary>
    public static class SynthSounds
    {
        public static AudioClip Pew, ShieldHit, Impact, Whoosh, Punch, ModeOn, ModeOff;
        const int Rate = 44100;
        static bool built;

        public static void Build()
        {
            if (built) return;
            built = true;
            var rnd = new System.Random(42);
            Pew = Make("sfx_pew", 0.20f, (t, n) =>
            {
                // 高い音から下がる（1900Hz → 約 460Hz の指数的な掃引）
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 16f);
                float ph = 2f * Mathf.PI * (1900f * 0.20f / Mathf.Log(0.24f)) * (Mathf.Pow(0.24f, t / 0.20f) - 1f);
                float tone = Mathf.Sin(ph) * 0.7f + Mathf.Sin(ph * 2.01f) * 0.18f;
                float noise = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 90f) * 0.35f;
                return (tone + noise) * env * 0.9f;
            });
            ShieldHit = Make("sfx_shield", 0.42f, (t, n) =>
            {
                float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t * 7.5f);
                float shimmer = Mathf.Sin(2f * Mathf.PI * (1250f + 180f * Mathf.Sin(t * 38f)) * t) * 0.35f
                              + Mathf.Sin(2f * Mathf.PI * 1870f * t) * 0.18f;
                float thump = Mathf.Sin(2f * Mathf.PI * (180f - 90f * Mathf.Min(1f, t / 0.12f)) * t) * Mathf.Exp(-t * 22f) * 0.6f;
                float zap = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 60f) * 0.4f;
                return (shimmer * env + thump + zap) * 0.85f;
            });
            Impact = Make("sfx_impact", 0.25f, (t, n) =>
            {
                float crack = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 45f);
                float body = Mathf.Sin(2f * Mathf.PI * 140f * t) * Mathf.Exp(-t * 20f) * 0.5f;
                return (crack * 0.6f + body) * 0.8f;
            });
            float lp = 0f;
            Whoosh = Make("sfx_whoosh", 0.26f, (t, n) =>
            {
                float x = (float)rnd.NextDouble() * 2f - 1f;
                float cut = 0.02f + 0.25f * Mathf.Sin(Mathf.Clamp01(t / 0.26f) * Mathf.PI);   // 風を切る音（こもった→明るい→こもった）
                lp += (x - lp) * cut;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.26f) * Mathf.PI);
                return lp * env * 1.6f;
            });
            Punch = Make("sfx_punch", 0.22f, (t, n) =>
            {
                float f = 95f - 40f * Mathf.Min(1f, t / 0.1f);
                float body = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 18f);
                float click = ((float)rnd.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 120f) * 0.7f;
                return (body + click) * 0.9f;
            });
            ModeOn = Make("sfx_mode_on", 0.26f, (t, n) =>
            {
                float f = t < 0.11f ? 660f : 990f;
                float env = Mathf.Exp(-((t < 0.11f ? t : t - 0.11f)) * 14f) * Mathf.Min(1f, (t < 0.11f ? t : t - 0.11f) / 0.004f);
                return Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.45f;
            });
            ModeOff = Make("sfx_mode_off", 0.26f, (t, n) =>
            {
                float f = t < 0.11f ? 990f : 620f;
                float env = Mathf.Exp(-((t < 0.11f ? t : t - 0.11f)) * 14f) * Mathf.Min(1f, (t < 0.11f ? t : t - 0.11f) / 0.004f);
                return Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.45f;
            });
        }

        static AudioClip Make(string name, float seconds, System.Func<float, int, float> fn)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float peak = 1e-6f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float fadeOut = Mathf.Clamp01((n - i) / (Rate * 0.01f));      // 最後の 10ms で静かに
                data[i] = fn(t, i) * fadeOut;
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            // 音割れしないよう、いちばん大きいところを 0.85 にそろえる（音の大きさの差は鳴らすときの音量で付ける）
            float g = 0.85f / peak;
            for (int i = 0; i < n; i++) data[i] *= g;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
