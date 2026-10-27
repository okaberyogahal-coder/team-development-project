using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// ミライフの体験全体（3D空間の上に重なるパネルと、空間内の案内）。
    ///
    /// 仕様 v0.17 の Unity 側の責務（3D描画・入力・視点・参加者表示・案内の選択）に加えて、
    /// 本来 Web パネルが担う画面（教材・講座予約・投稿・未来ナビ・プロフィール）を
    /// 「流れを確かめるためのデモ画面」として Unity 内に用意している。
    /// Webのログイン・プロフィールを反映。保存・実AI・通話・アップロードはデモ。
    ///
    /// シニアの利用を前提に：明るい配色・大きな文字（本文22px相当以上）・大きなボタン（高さ56px以上）・
    /// 点滅なし・マウスだけで操作できる（クリック移動と画面下のボタン）。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public partial class MiraiWorld : MonoBehaviour
    {
        public static bool UIOpen { get; private set; }
        public static bool IntroShowing => instance != null && instance.panel == Panel.Intro;
        public static bool PointerOverUI { get; private set; }
        public static bool PromptVisible { get; private set; }
        static MiraiWorld instance;

        enum Panel { None, Intro, Learn, CourseDetail, Viewer, Teach, EditCourse, Connect, Navi, Profile, Person, Help }
        enum LearnTab { Courses, Sessions, Later }
        enum TeachTab { Materials, Lectures }

        Panel panel = Panel.Intro;
        Panel detailBack = Panel.Learn;
        LearnTab learnTab = LearnTab.Courses;
        TeachTab teachTab = TeachTab.Materials;

        public INaviProvider navi = new MockNaviProvider();

        MaterialLibrary lib;
        PlayerWalker player;
        Camera cam;
        MiraiCampus campus;

        readonly List<Citizen> citizens = new List<Citizen>();
        readonly List<MiraiProfile> people = new List<MiraiProfile>();
        List<Course> courses;
        List<LectureSession> sessions;
        readonly Dictionary<string, DateTime> completed = new Dictionary<string, DateTime>();
        readonly List<string> later = new List<string>();
        MiraiProfile me;
        WebPlayerSession webSession;
        Vector2 webProfileScroll;

        Course detailCourse;
        float viewerProgress;
        Course editTarget;
        string editTitle = "", editDesc = "", editCat = "暮らしの知恵";
        CourseKind editKind = CourseKind.Video;
        string editFileState = "未送信";
        float uploadStart = -1f;
        string deleteConfirmId;
        MiraiProfile personView;

        MiraiRoom currentRoom, pendingRoom;
        bool mic, camOn;
        MiraiGate nearGate;
        Citizen focus;

        NaviResult naviResult;
        float naviReadyAt = -1f;

        GameObject guideMarker, clickMarker;
        Vector3? guideTarget;
        string guideLabel;
        string toast;
        float toastUntil;
        Vector2 scroll;
        string catFilter = "すべて";
        float userScale = 1.15f;

        string lecTitle = "";
        int lecDay = 1, lecTime, lecCap = 7;
        static readonly string[] LecTimes = { "10:00", "13:30", "15:00" };

        string qName = "", qIssuer = "", qWhen = "";
        int avatarChoice;
        static readonly Color[] AvatarColors =
        {
            new Color(0.98f, 0.82f, 0.6f), new Color(0.6f, 0.82f, 1f), new Color(0.7f, 0.92f, 0.7f),
            new Color(1f, 0.72f, 0.8f), new Color(0.85f, 0.78f, 1f)
        };

        readonly List<Rect> uiRects = new List<Rect>();
        readonly List<Rect> uiRectsLast = new List<Rect>();
        bool clearFocus;

        // ================================================================== 初期化
        public void Init(MaterialLibrary library, PlayerWalker walker, Camera camera, MiraiCampus c)
        {
            instance = this;
            lib = library;
            player = walker;
            cam = camera;
            campus = c;
            me = MiraiData.Player();
            courses = MiraiData.Courses();
            courses.Add(MiraiData.MyDraft(me));
            sessions = MiraiData.Sessions(MiraiClock.Now);

            var root = new GameObject("DemoPeople");
            root.transform.SetParent(transform, false);
            foreach (var p in MiraiData.Citizens())
            {
                float scale = p.age < 12 ? 0.72f : p.age < 16 ? 0.9f : p.age >= 90 ? 0.94f : 1f;
                var go = MiraiAvatar.Build(lib, root.transform, "Demo_" + p.name, scale, p.color, Mat.FlowerWhite, true, p.age);
                go.transform.position = campus.W(p.spot.x, p.spot.y, MiraiCampus.DeckTop + 0.12f);
                var ct = go.AddComponent<Citizen>();
                ct.profile = p;
                ct.halo = go.transform.Find("Halo");
                ct.player = walker.transform;
                ct.homeYaw = p.facing;
                go.layer = PlayerWalker.PeopleLayer;   // カメラの障害物判定から外す（クリックで話しかけるのは可能）
                citizens.Add(ct);
                people.Add(p);
            }

            guideMarker = BuildMarker("GuideMarker", 3.2f, Mat.SignGreen);
            clickMarker = BuildMarker("ClickMarker", 0f, Mat.SignBlue);
            guideMarker.SetActive(false);
            clickMarker.SetActive(false);
            ApplyAvatarColor();
            SetPanel(Panel.Intro);
            webProfile = gameObject.AddComponent<WebProfileBridge>();
            webProfile.Init(this);
            WebSessionBridge.Attach(this);
        }

        public AppliedWebPlayer ApplyWebSession(WebPlayerSession session)
        {
            bool authenticated = session.authenticated && session.profile != null;
            string userId = authenticated ? session.profile.userId : "";
            string oldUserId = webSession != null && webSession.authenticated && webSession.profile != null ? webSession.profile.userId : "";
            bool changed = webSession == null || oldUserId != userId;
            webSession = session;
            me = new MiraiProfile
            {
                name = authenticated ? session.profile.nickname : "ゲスト",
                age = 0,
                bio = "",
                aiUsesAge = false,
                teaches = new List<string>(authenticated ? session.profile.teaches ?? new string[0] : new string[0]),
                learns = new List<string>(authenticated ? session.profile.wantsToLearn ?? new string[0] : new string[0]),
                color = WebSessionBridge.AvatarColor(authenticated ? session.profile.avatarId : "sprout")
            };
            if (changed)
            {
                completed.Clear();
                later.Clear();
                webProfileScroll = Vector2.zero;
                courses = MiraiData.Courses();
                sessions = MiraiData.Sessions(MiraiClock.Now);
                naviResult = null;
                naviReadyAt = uploadStart = -1f;
                detailCourse = editTarget = null;
                personView = null;
                currentRoom = pendingRoom = null;
                mic = camOn = false;
                toast = guideLabel = null;
                guideTarget = null;
                if (guideMarker != null) guideMarker.SetActive(false);
                if (player != null) { player.StopAuto(); PlayerWalker.SetFreeLook(false); }
                SetPanel(Panel.Intro);
            }
            ApplyAvatarColor();
            return new AppliedWebPlayer
            {
                revision = session.revision,
                authenticated = authenticated,
                state = authenticated ? "authenticated" : session.status == "unavailable" ? "unavailable" : "guest",
                userId = userId,
                nickname = me.name,
                avatarId = authenticated ? session.profile.avatarId : "sprout",
                avatarLabel = WebSessionBridge.AvatarLabel(authenticated ? session.profile.avatarId : "sprout"),
                suitColor = ColorUtility.ToHtmlStringRGB(me.color),
                teaches = me.teaches.ToArray(),
                wantsToLearn = me.learns.ToArray()
            };

        }

        GameObject BuildMarker(string name, float height, Mat color)
        {
            var mb = new MeshBuilder();
            mb.AddRing(Vector3.up * 0.02f, 0.75f, 0.95f, 0.04f, 40, (int)color);
            if (height > 0f)
            {
                mb.AddExtrusion(Vector3.zero, MeshBuilder.Circle(0.07f, 10), MeshBuilder.Circle(0.07f, 10), height, 0f, (int)color, (int)color, new Vector2(4f, 4f), true, false);
                mb.AddExtrusion(Vector3.up * height, MeshBuilder.Circle(0.45f, 4), MeshBuilder.Circle(0.01f, 4), 0.6f, 0f, (int)color, (int)color, new Vector2(4f, 4f), false, false);
                mb.AddExtrusion(Vector3.up * height, MeshBuilder.Circle(0.45f, 4), MeshBuilder.Circle(0.01f, 4), -0.6f, 0f, (int)color, (int)color, new Vector2(4f, 4f), false, false);
            }
            var go = mb.Build(name, transform, lib, ShadowCastingMode.Off);
            if (height > 0f)
            {
                var h = go.AddComponent<Hologram>();
                h.spinSpeed = 40f;
                h.flicker = false;
            }
            return go;
        }

        void ApplyAvatarColor(bool choosePreset = false)
        {
            me.color = webSession != null && webSession.authenticated
                ? WebSessionBridge.AvatarColor(webSession.profile.avatarId)
                : webSession != null ? WebSessionBridge.AvatarColor("sprout") : AvatarColors[avatarChoice];
            if (player == null || player.body == null) return;
            var imported = player.body.GetComponent<ImportedAvatar>();
            if (imported != null)
            {
                if (choosePreset)
                {
                    imported.SelectVariant(avatarChoice);
                    if (imported.Appearance != null) AvatarWardrobePreferences.Save(imported.Appearance);
                }
                return;
            }
            PersonFactory.SetSuitColor(player.body, me.color);   // 胴と両腕の服の色
        }

        void SetPanel(Panel p)
        {
            if (panel == Panel.Viewer && p != Panel.Viewer) viewerProgress = 0f; // 閉じたら再生を止める
            panel = p;
            UIOpen = p != Panel.None;
            scroll = Vector2.zero;
            deleteConfirmId = null;
            clearFocus = true;
        }

        void OnDestroy()
        {
            DisposeAvatarTheme();
            if (instance == this) { instance = null; UIOpen = false; PointerOverUI = false; PromptVisible = false; }
        }

        // ================================================================== 更新
        void Update()
        {
            float uiS = UiScale();
            Vector2 mp = InputBridge.MousePosition;
            var gp = new Vector2(mp.x / uiS, (Screen.height - mp.y) / uiS);
            bool over = false;
            foreach (var r in uiRectsLast) if (r.Contains(gp)) { over = true; break; }
            PointerOverUI = UIOpen || over;

            if (creatingWebAvatar) return;

            // 擬似アップロード（未送信 → 送信中 → 検証中 → 利用可能）
            if (uploadStart > 0f)
            {
                float t = Time.time - uploadStart;
                editFileState = t < 1.2f ? "送信中" : t < 2.6f ? "検証中" : "利用可能";
                if (t >= 2.6f) uploadStart = -1f;
            }
            if (naviReadyAt > 0f && Time.time >= naviReadyAt)
            {
                naviReadyAt = -1f;
                naviResult = navi.Suggest(me, courses, completed.Keys);
            }
            if (panel == Panel.Viewer && viewerProgress < 1f) viewerProgress = Mathf.Min(1f, viewerProgress + Time.deltaTime / 8f);

            if (panel == Panel.Intro)
            {
                if (InputBridge.Down(FKey.Enter)) StartPlaza();
                return;
            }
            if (panel != Panel.None)
            {
                if (InputBridge.Down(FKey.Escape)) Back();
                else if (GUIUtility.keyboardControl == 0) MenuKeys();   // 文字を入力中は切り替えない
                return;
            }

            Vector3 pp = player.transform.position;

            // 交流室：中に入ったら「入室する」を案内、外に出たらロビーへ戻る
            MiraiRoom inside = campus.Rooms.Find(r => r.Contains(pp));
            if (currentRoom != null && inside != currentRoom) LeaveRoom(false);
            pendingRoom = currentRoom == null ? inside : null;

            // 機能の入口：近づくと説明だけ（仕様 AC-203-02）。開くのは明示的な選択
            nearGate = null;
            if (inside == null)
                foreach (var g in campus.Gates)
                    if (FlatDist(g.pos, pp) < 5.5f) { nearGate = g; break; }

            focus = null;
            float best = 3.2f;
            foreach (var c in citizens)
            {
                float d = FlatDist(c.transform.position, pp);
                if (d < best) { best = d; focus = c; }
            }
            PromptVisible = focus != null || pendingRoom != null || nearGate != null;

            if (InputBridge.Down(FKey.E) || InputBridge.Down(FKey.Enter)) ActivatePrompt();
            else if (MenuKeys()) { }
            else if (InputBridge.Down(FKey.N)) OpenFeature(MiraiFeature.Navi);
            else if (InputBridge.Down(FKey.P)) SetPanel(Panel.Profile);
            else if (InputBridge.Down(FKey.H)) SetPanel(Panel.Help);

            // クリック移動・人をクリックして話しかける
            // ドラッグ（見回し）ではなく「クリックして離した」ときだけ歩く・話しかける
            if (InputBridge.LeftMouseUp && !player.LastPressWasDrag && (PlayerWalker.FreeLook || !PointerOverUI) && cam != null)
            {
                // 見回しモードでは画面中央の照準、カーソルを出しているときはマウスの位置
                Vector2 aim = PlayerWalker.FreeLook ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) : mp;
                var ray = cam.ScreenPointToRay(new Vector3(aim.x, aim.y, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, 250f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var ct = hit.collider.GetComponentInParent<Citizen>();
                    if (ct != null)
                    {
                        Vector3 dir = pp - ct.transform.position; dir.y = 0f;
                        Vector3 stand = ct.transform.position + (dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.back) * 1.6f;
                        var target = ct;
                        player.MoveTo(stand, () => { personView = target.profile; SetPanel(Panel.Person); });
                        ShowClick(stand);
                    }
                    else if (hit.normal.y > 0.5f)
                    {
                        player.MoveTo(hit.point);
                        ShowClick(hit.point);
                    }
                }
            }
            if (clickMarker.activeSelf && !player.AutoMoving) clickMarker.SetActive(false);

            if (guideTarget.HasValue)
            {
                guideMarker.transform.position = guideTarget.Value;
                if (FlatDist(guideTarget.Value, pp) < 3f)
                {
                    Toast($"{guideLabel}に着きました。");
                    StopGuide();
                }
            }
        }

        static float FlatDist(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        void ShowClick(Vector3 p)
        {
            clickMarker.transform.position = p + Vector3.up * 0.02f;
            clickMarker.SetActive(true);
        }

        void StartPlaza()
        {
            ApplyAvatarColor();
            SetPanel(Panel.None);
            Toast("ミライフ広場へようこそ。マウスで見回し、W A S D で歩けます。1〜6 キーで下のメニューを開けます。");
        }

        void ActivatePrompt()
        {
            if (focus != null) { personView = focus.profile; SetPanel(Panel.Person); }
            else if (pendingRoom != null) EnterRoom(pendingRoom);
            else if (nearGate != null) OpenFeature(nearGate.feature);
        }

        /// <summary>下のメニューを数字キーで開く：1 学ぶ／2 教える／3 つながる／4 未来ナビ／5 プロフィール／6 ヘルプ</summary>
        bool MenuKeys()
        {
            FKey[] keys = { FKey.One, FKey.Two, FKey.Three, FKey.Four, FKey.Five, FKey.Six };
            for (int i = 0; i < keys.Length; i++)
            {
                if (!InputBridge.Down(keys[i])) continue;
                OpenMenu(i);
                return true;
            }
            return false;
        }

        void OpenMenu(int i)
        {
            if (i < 4) OpenFeature((MiraiFeature)i);
            else if (i == 4) SetPanel(Panel.Profile);
            else SetPanel(Panel.Help);
        }

        void OpenFeature(MiraiFeature f)
        {
            switch (f)
            {
                case MiraiFeature.Learn: SetPanel(Panel.Learn); break;
                case MiraiFeature.Teach: SetPanel(Panel.Teach); break;
                case MiraiFeature.Connect: SetPanel(Panel.Connect); break;
                default: SetPanel(Panel.Navi); break;
            }
        }

        void Back()
        {
            switch (panel)
            {
                case Panel.CourseDetail: SetPanel(detailBack); break;
                case Panel.Viewer: SetPanel(Panel.CourseDetail); break;
                case Panel.EditCourse: SetPanel(Panel.Teach); break;
                default: SetPanel(Panel.None); break;
            }
        }

        void Toast(string msg)
        {
            toast = msg;
            toastUntil = Time.time + 6f;
        }

        void Guide(Vector3 target, string label)
        {
            guideTarget = target;
            guideLabel = label;
            guideMarker.SetActive(true);
            guideMarker.transform.position = target;
            player.MoveTo(target);
            ShowClick(target);
            Toast($"{label}へ案内します。そのまま待つと自動で歩きます。");
        }

        void StopGuide()
        {
            guideTarget = null;
            guideMarker.SetActive(false);
        }

        // ---- 交流室
        void EnterRoom(MiraiRoom r)
        {
            currentRoom = r;
            pendingRoom = null;
            mic = false;
            camOn = false;
            Toast($"{r.name}に入りました（{RoomCount(r)}/8人）。マイクとカメラはOFFです。");
        }

        void LeaveRoom(bool teleportOut)
        {
            var r = currentRoom;
            currentRoom = null;
            mic = false;
            camOn = false;
            if (teleportOut && r != null)
            {
                Vector3 d = campus.Center - r.door; d.y = 0f;
                player.Teleport(r.door + Vector3.up * 0.3f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            }
            Toast("ロビーに戻りました。ロビーでは通話はありません。");
        }

        /// <summary>部屋の使用枠。デモ人物は数えない（仕様 §9）。このデモの実参加者はあなただけ。</summary>
        int RoomCount(MiraiRoom r) => currentRoom == r ? 1 : 0;

        // ---- 学ぶ
        void OpenCourse(Course c, Panel back)
        {
            detailCourse = c;
            detailBack = back;
            SetPanel(Panel.CourseDetail);
        }

        void Complete(Course c)
        {
            completed[c.id] = MiraiClock.Now;
            Toast($"「{c.title}」を受講完了にしました。未来ナビで次の学びを探せます。");
        }

        void Book(LectureSession s)
        {
            var now = MiraiClock.Now;   // 本番はサーバー時刻で判定する（仕様 FR-103）
            if (!s.CanBook(now))
            {
                Toast(now >= s.ClosesAt ? "予約の締切（開始30分前）を過ぎたため予約できません。" :
                      s.SeatsLeft == 0 ? "満席のため予約できません。" : "この講座は予約できません。");
                return;
            }
            s.booked++;
            s.myBooking = true;
            Toast($"「{s.title}」を予約しました。{MiraiData.FormatDate(s.startsAt)}から、{s.room}で行います。");
        }

        void CancelBooking(LectureSession s)
        {
            if (!s.myBooking) return;
            // 取消期限は未決（仕様 Q-21）。この試作では仮に「開始前まで」取り消せるようにしている
            if (MiraiClock.Now >= s.startsAt) { Toast("開始後は取り消せません（取消期限は仕様で未決のため、試作では開始前までとしています）。"); return; }
            s.myBooking = false;
            s.booked = Mathf.Max(0, s.booked - 1);
            Toast("予約を取り消しました。");
        }

        // ---- 教える
        void StartEdit(Course c)
        {
            editTarget = c;
            editTitle = c != null ? c.title : "";
            editDesc = c != null ? c.description : "";
            editCat = c != null ? c.category : "暮らしの知恵";
            editKind = c != null ? c.kind : CourseKind.Video;
            editFileState = c != null ? c.fileState : "未送信";
            uploadStart = -1f;
            SetPanel(Panel.EditCourse);
        }

        string EditProblem()
        {
            string t = editTitle.Trim(), d = editDesc.Trim();
            if (t.Length == 0) return "題名を入れてください。";
            if (t.Length > 100) return "題名は100文字までです。";
            if (d.Length == 0) return "説明を入れてください。";
            if (d.Length > 2000) return "説明は2000文字までです。";
            return null;
        }

        void SaveEdit(bool publish)
        {
            string prob = EditProblem();
            if (prob != null) { Toast(prob); return; }
            if (publish && editFileState != "利用可能") { Toast("公開するには、ファイルの確認が終わるまでお待ちください。"); return; }
            var c = editTarget;
            if (c == null)
            {
                c = new Course { id = "m" + (courses.Count + 1), mine = true, author = me.name, published = false };
                courses.Add(c);
            }
            c.title = editTitle.Trim();
            c.description = editDesc.Trim();
            c.category = editCat;
            c.kind = editKind;
            c.fileState = editFileState;
            c.author = me.name;
            c.authorAge = me.age;
            c.minutes = c.minutes > 0 ? c.minutes : 10;
            if (c.tags.Count == 0 && me.teaches.Count > 0) c.tags.Add(me.teaches[0]);
            if (publish) c.published = true;
            Toast(publish ? $"「{c.title}」を公開しました。「学ぶ」の一覧に表示されます。" : "下書きを保存しました。");
            SetPanel(Panel.Teach);
        }

        // ================================================================== GUI：スタイル
        GUIStyle sPanel, sCard, sTitle, sH2, sBody, sSmall, sBtn2, sChip, sTag, sInput, sLabelBig, sCenter;
        readonly Dictionary<Color, GUIStyle> btnStyles = new Dictionary<Color, GUIStyle>();
        readonly Dictionary<Color, Texture2D> solid = new Dictionary<Color, Texture2D>();

        static readonly Color Ink = new Color(0.16f, 0.16f, 0.18f);
        static readonly Color SubInk = new Color(0.36f, 0.36f, 0.4f);
        static readonly Color Cream = new Color(1f, 0.988f, 0.965f);
        static readonly Color Line = new Color(0.86f, 0.82f, 0.75f);
        static readonly Color Muted = new Color(0.62f, 0.62f, 0.64f);

        static Texture2D Rounded(Color fill, Color border, int radius, int bw)
        {
            const int size = 48;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                Color c = d > -bw ? border : fill;
                c.a *= Mathf.Clamp01(0.5f - d);
                px[y * size + x] = c;
            }
            t.SetPixels(px);
            t.Apply(false, false);
            return t;
        }

        Texture2D Solid(Color c)
        {
            if (solid.TryGetValue(c, out var t)) return t;
            t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            t.SetPixels(new[] { c, c, c, c });
            t.Apply(false, false);
            solid[c] = t;
            return t;
        }

        GUIStyle Txt(int size, bool bold, Color c)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, richText = true, wordWrap = true };
            s.normal.textColor = c;
            return s;
        }

        Font japaneseFont;

        void Styles()
        {
            // Web builds cannot fall back to Japanese fonts installed on the user's OS.
            if (japaneseFont == null) japaneseFont = Resources.Load<Font>("Fonts/NotoSansCJKjp-Regular");
            if (japaneseFont != null) GUI.skin.font = japaneseFont;
            if (sPanel != null) return;
            sPanel = new GUIStyle(GUI.skin.box) { border = new RectOffset(18, 18, 18, 18), padding = new RectOffset(0, 0, 0, 0) };
            sPanel.normal.background = Rounded(Cream, Line, 18, 2);
            sCard = new GUIStyle(GUI.skin.button) { border = new RectOffset(14, 14, 14, 14), alignment = TextAnchor.UpperLeft };
            sCard.normal.background = Rounded(Color.white, Line, 14, 2);
            sCard.hover.background = Rounded(new Color(1f, 0.97f, 0.9f), new Color(0.9f, 0.7f, 0.4f), 14, 3);
            sCard.active.background = sCard.hover.background;

            sTitle = Txt(34, true, Ink);
            sH2 = Txt(25, true, Ink);
            sBody = Txt(22, false, Ink);
            sSmall = Txt(19, false, SubInk);
            sLabelBig = Txt(30, true, Ink);
            sLabelBig.alignment = TextAnchor.MiddleCenter;
            sCenter = Txt(22, false, Ink);
            sCenter.alignment = TextAnchor.MiddleCenter;

            sBtn2 = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold, richText = true, wordWrap = true, border = new RectOffset(14, 14, 14, 14) };
            sBtn2.normal.background = Rounded(Color.white, new Color(0.55f, 0.55f, 0.58f), 14, 2);
            sBtn2.hover.background = Rounded(new Color(0.96f, 0.96f, 0.96f), new Color(0.3f, 0.3f, 0.33f), 14, 3);
            sBtn2.active.background = sBtn2.hover.background;
            sBtn2.normal.textColor = Ink;
            sBtn2.hover.textColor = Ink;

            sChip = new GUIStyle(GUI.skin.box) { fontSize = 19, richText = true, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(14, 14, 6, 6), border = new RectOffset(14, 14, 14, 14) };
            sChip.normal.background = Rounded(Color.white, Line, 14, 2);
            sChip.normal.textColor = Ink;

            sTag = new GUIStyle(GUI.skin.box) { fontSize = 19, richText = true, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(14, 14, 8, 8), border = new RectOffset(14, 14, 14, 14) };
            sTag.normal.background = Rounded(new Color(1f, 1f, 1f, 0.94f), Line, 14, 2);
            sTag.normal.textColor = Ink;

            sInput = new GUIStyle(GUI.skin.textField) { fontSize = 22, padding = new RectOffset(12, 12, 10, 10), border = new RectOffset(10, 10, 10, 10), wordWrap = true };
            sInput.normal.background = Rounded(Color.white, new Color(0.5f, 0.5f, 0.55f), 10, 2);
            sInput.focused.background = Rounded(Color.white, new Color(0.2f, 0.5f, 0.85f), 10, 3);
            sInput.normal.textColor = Ink;
            sInput.focused.textColor = Ink;
        }

        GUIStyle Primary(Color c)
        {
            if (btnStyles.TryGetValue(c, out var s)) return s;
            s = new GUIStyle(sBtn2);
            s.normal.background = Rounded(c, c * 0.85f, 14, 2);
            s.hover.background = Rounded(Color.Lerp(c, Color.white, 0.15f), c * 0.7f, 14, 3);
            s.active.background = s.hover.background;
            s.normal.textColor = Color.white;
            s.hover.textColor = Color.white;
            btnStyles[c] = s;
            return s;
        }

        void Track(Rect r)
        {
            if (Event.current != null && Event.current.type == EventType.Repaint) uiRects.Add(r);
        }

        bool Btn(Rect r, string text, Color? color = null, bool enabled = true)
        {
            Track(r);
            bool old = GUI.enabled;
            GUI.enabled = enabled;
            bool clicked = GUI.Button(r, text, ButtonStyle(color));
            GUI.enabled = old;
            return clicked && enabled;
        }

        void Box(Rect r, GUIStyle st)
        {
            Track(r);
            GUI.Box(r, "", st);
        }

        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        float UiScale() => creatingWebAvatar ? AvatarWardrobeLayout.ScaleForViewport(Screen.width, Screen.height) : Mathf.Max(0.6f, Screen.height / 1080f) * userScale;

        // ================================================================== GUI：描画
        float W, H;

        void OnGUI()
        {
            if (cam == null || campus == null) return;
            Styles();
            if (Event.current.type == EventType.Repaint) uiRects.Clear();
            if (clearFocus) { GUI.FocusControl(null); clearFocus = false; }

            float s = UiScale();
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            W = Screen.width / s;
            H = Screen.height / s;

            if (creatingWebAvatar) { DrawWebAvatarCreation(); FinishFrame(); return; }
            if (panel == Panel.Intro) { DrawIntro(); FinishFrame(); return; }

            DrawWorldLabels(s);
            DrawStatus();
            DrawGuideBar();
            if (panel == Panel.None)
            {
                DrawPrompt();
                DrawBottomBar();
                if (PlayerWalker.FreeLook) DrawAim();
            }
            DrawToast();

            switch (panel)
            {
                case Panel.Learn: DrawLearn(); break;
                case Panel.CourseDetail: DrawCourseDetail(); break;
                case Panel.Viewer: DrawViewer(); break;
                case Panel.Teach: DrawTeach(); break;
                case Panel.EditCourse: DrawEditCourse(); break;
                case Panel.Connect: DrawConnect(); break;
                case Panel.Navi: DrawNavi(); break;
                case Panel.Profile: DrawProfile(); break;
                case Panel.Person: DrawPerson(); break;
                case Panel.Help: DrawHelp(); break;
            }
            FinishFrame();
        }

        /// <summary>画面中央の小さな照準（ここを見てクリックすると、人に話しかけたり、そこまで歩いたりできる）</summary>
        void DrawAim()
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(W * 0.5f - 5f, H * 0.5f - 5f, 10f, 10f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.95f);
            GUI.DrawTexture(new Rect(W * 0.5f - 3f, H * 0.5f - 3f, 6f, 6f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        void FinishFrame()
        {
            if (Event.current.type == EventType.Repaint)
            {
                uiRectsLast.Clear();
                uiRectsLast.AddRange(uiRects);
            }
        }

        bool ToGui(Vector3 world, float s, out Vector2 p)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            p = new Vector2(sp.x / s, (Screen.height - sp.y) / s);
            return sp.z > 0.5f;
        }

        void TagAt(Vector2 p, string text, bool big = false)
        {
            var st = big ? new GUIStyle(sTag) { fontSize = 26 } : sTag;
            var size = st.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(p.x - size.x * 0.5f, p.y - size.y, size.x, size.y), text, st);
        }

        // ---- 空間の案内ラベル
        void DrawWorldLabels(float s)
        {
            Vector3 camPos = cam.transform.position;
            foreach (var g in campus.Gates)
            {
                Vector3 w = g.pos + Vector3.up * 6.6f;
                float d = Vector3.Distance(camPos, w);
                if (d > 70f || !ToGui(w, s, out var p)) continue;
                Color fc = MiraiData.FeatureColor(g.feature);
                TagAt(p, $"<b><color={Hex(fc)}>{MiraiData.FeatureName(g.feature)}</color></b>\n<size=18>{MiraiData.FeatureCaption(g.feature)}</size>", true);
            }
            foreach (var r in campus.Rooms)
            {
                Vector3 w = r.door + Vector3.up * 5.2f;
                float d = Vector3.Distance(camPos, w);
                if (d > 75f || !ToGui(w, s, out var p)) continue;
                TagAt(p, $"<b><color={Hex(r.color * 0.8f)}>{r.name}</color></b>  <size=18>{RoomCount(r)}/8人</size>");
            }
            if (ToGui(campus.W(0f, -campus.Half + 3f, 7.2f), s, out var wp) && Vector3.Distance(camPos, campus.W(0f, -campus.Half + 3f)) < 60f)
                TagAt(wp, "<b>ようこそ　ミライフへ</b>", true);
            if (ToGui(campus.W(0f, 0f, 4.4f), s, out var lp) && Vector3.Distance(camPos, campus.Center) < 40f)
                TagAt(lp, "<b>ロビー</b>  <size=18>（通話はありません）</size>");

            foreach (var c in citizens)
            {
                float d = Vector3.Distance(camPos, c.HeadPosition);
                if (d > 14f || !ToGui(c.HeadPosition + Vector3.up * 0.3f, s, out var p)) continue;
                var pr = c.profile;
                TagAt(p, $"<b>{pr.name}さん</b> {pr.age}歳 <size=16><color=#888888>デモ</color></size>");
            }
        }

        // ---- 左上：現在地と参加人数
        void DrawStatus()
        {
            var r = new Rect(16f, 16f, 430f, (currentRoom != null ? 200f : 104f) + (webSession != null ? 38f : 0f));
            Box(r, sPanel);
            string where = currentRoom != null ? $"{currentRoom.name}（{RoomCount(currentRoom)}/8人）" : "ロビー・庭";
            GUI.Label(new Rect(r.x + 20f, r.y + 12f, r.width - 40f, 36f), $"<b>現在地：</b>{where}", sBody);
            GUI.Label(new Rect(r.x + 20f, r.y + 52f, r.width - 40f, 36f), "広場の参加者：<b>1</b>/50人（あなた）", sSmall);
            if (webSession != null)
                GUI.Label(new Rect(r.x + 20f, r.yMax - 40f, r.width - 40f, 36f), webSession.authenticated ? $"{Plain(me.name)}さんとして参加中" : "ゲストとして見学中", sSmall);
            if (currentRoom != null)
            {
                float bw = (r.width - 52f) / 2f;
                if (Btn(new Rect(r.x + 20f, r.y + 92f, bw, 50f), mic ? "マイク ON" : "マイク OFF", mic ? new Color(0.85f, 0.3f, 0.3f) : (Color?)null))
                { mic = !mic; Toast(mic ? "マイクをONにしました（デモ：通話サービス未接続）" : "マイクをOFFにしました"); }
                if (Btn(new Rect(r.x + 32f + bw, r.y + 92f, bw, 50f), camOn ? "カメラ ON" : "カメラ OFF", camOn ? new Color(0.85f, 0.3f, 0.3f) : (Color?)null))
                { camOn = !camOn; Toast(camOn ? "カメラをONにしました（デモ：通話サービス未接続）" : "カメラをOFFにしました"); }
                if (Btn(new Rect(r.x + 20f, r.y + 146f, r.width - 40f, 44f), "ロビーへ戻る")) LeaveRoom(true);
            }
        }

        static readonly string[] Arrows = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };

        void DrawGuideBar()
        {
            if (!guideTarget.HasValue) return;
            Vector3 d = guideTarget.Value - player.transform.position;
            d.y = 0f;
            float rel = Mathf.DeltaAngle(cam.transform.eulerAngles.y, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            int idx = ((Mathf.RoundToInt(rel / 45f) % 8) + 8) % 8;
            string text = $"案内中：<b>{guideLabel}</b>　<size=30><b>{Arrows[idx]}</b></size>　あと{d.magnitude:0}m";
            var size = sTag.CalcSize(new GUIContent(text));
            var r = new Rect((W - size.x - 190f) * 0.5f, 16f, size.x, 56f);
            Box(r, sTag);
            GUI.Label(r, text, sCenter);
            if (Btn(new Rect(r.xMax + 12f, 16f, 178f, 56f), "案内をやめる")) { StopGuide(); player.StopAuto(); }
        }

        void DrawPrompt()
        {
            string title = null, sub = null, button = null;
            Color col = new Color(0.25f, 0.55f, 0.35f);
            if (focus != null)
            {
                title = $"{focus.profile.name}さん（{focus.profile.age}歳）";
                sub = "プロフィールを見ることができます（デモ人物）";
                button = "プロフィールを見る（E）";
            }
            else if (pendingRoom != null)
            {
                title = pendingRoom.name;
                sub = $"{pendingRoom.caption}。いま {RoomCount(pendingRoom)}/8人。入室してもマイク・カメラはOFFのままです。";
                button = "入室する（E）";
                col = pendingRoom.color * 0.85f;
            }
            else if (nearGate != null)
            {
                title = MiraiData.FeatureName(nearGate.feature);
                sub = MiraiData.FeatureCaption(nearGate.feature);
                button = "ひらく（E）";
                col = MiraiData.FeatureColor(nearGate.feature);
            }
            if (title == null) return;
            float w = Mathf.Min(900f, W - 40f);
            var r = new Rect((W - w) * 0.5f, H - 120f - 118f, w, 104f);
            Box(r, sPanel);
            GUI.Label(new Rect(r.x + 24f, r.y + 12f, w - 300f, 40f), $"<b><color={Hex(col)}>{title}</color></b>", sH2);
            GUI.Label(new Rect(r.x + 24f, r.y + 52f, w - 300f, 50f), sub, sSmall);
            if (Btn(new Rect(r.xMax - 264f, r.y + 22f, 240f, 60f), button, col)) ActivatePrompt();
        }

        void DrawBottomBar()
        {
            var items = new[] { "<b>1</b>  学ぶ", "<b>2</b>  教える", "<b>3</b>  つながる", "<b>4</b>  未来ナビ", "<b>5</b>  プロフィール", "<b>6</b>  ヘルプ" };
            float bw = Mathf.Min(190f, (W - 40f - 5f * 12f) / 6f), bh = 78f;
            float total = bw * 6f + 12f * 5f;
            float x = (W - total) * 0.5f, y = H - bh - 22f;
            Box(new Rect(x - 14f, y - 14f, total + 28f, bh + 28f), sPanel);
            for (int i = 0; i < 6; i++)
            {
                Color? col = i < 4 ? MiraiData.FeatureColor((MiraiFeature)i) : (Color?)null;
                if (Btn(new Rect(x + i * (bw + 12f), y, bw, bh), items[i], col))
                {
                    OpenMenu(i);
                }
            }
        }

        void DrawToast()
        {
            if (string.IsNullOrEmpty(toast) || Time.time > toastUntil) return;
            float w = Mathf.Min(1000f, W - 40f);
            var r = new Rect((W - w) * 0.5f, panel == Panel.None ? H - 336f : H - 96f, w, 70f);
            Box(r, sTag);
            GUI.Label(new Rect(r.x + 16f, r.y, r.width - 32f, r.height), toast, sCenter);
        }

        // ---- パネルの枠
        Rect Frame(string title, Color accent, float w, float h, string backLabel = "閉じる", bool showBack = true)
        {
            w = Mathf.Min(w, W - 30f);
            h = Mathf.Min(h, H - 30f);
            var r = new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h);
            Box(r, sPanel);
            GUI.DrawTexture(new Rect(r.x + 26f, r.y + 26f, 10f, 44f), Solid(accent));
            GUI.Label(new Rect(r.x + 48f, r.y + 22f, w - 320f, 54f), $"<b>{title}</b>", sTitle);
            if (showBack && Btn(new Rect(r.xMax - 226f, r.y + 20f, 200f, 58f), $"{backLabel}（Esc）")) Back();
            return new Rect(r.x + 30f, r.y + 96f, w - 60f, h - 120f);
        }

        bool Tab(Rect r, string text, bool on, Color accent) => Btn(r, on ? $"● {text}" : text, on ? accent : (Color?)null);

        float Chips(float x, float y, float maxW, IList<string> items, Color tint)
        {
            float cx = x, cy = y, rowH = 0f;
            foreach (var it in items)
            {
                var size = sChip.CalcSize(new GUIContent(it));
                if (cx + size.x > x + maxW && cx > x) { cx = x; cy += rowH + 8f; rowH = 0f; }
                var old = GUI.backgroundColor;
                GUI.backgroundColor = tint;
                GUI.Box(new Rect(cx, cy, size.x, size.y), it, sChip);
                GUI.backgroundColor = old;
                cx += size.x + 8f;
                rowH = Mathf.Max(rowH, size.y);
            }
            return items.Count == 0 ? y : cy + rowH;
        }

        // ================================================================== 紹介・入場
        void DrawIntro()
        {
            GUI.DrawTexture(new Rect(0f, 0f, W, H), Solid(new Color(1f, 0.98f, 0.94f, 0.55f)));
            float w = Mathf.Min(1180f, W - 30f), h = Mathf.Min(860f, H - 30f);
            var r = new Rect((W - w) * 0.5f, (H - h) * 0.5f, w, h);
            Box(r, sPanel);
            float x = r.x + 46f, y = r.y + 34f, iw = w - 92f;
            GUI.Label(new Rect(x, y, iw, 64f), "<size=52><b><color=#2E7D4F>ミライフ</color></b></size>　<size=22>MIRAI LIFE</size>", sTitle);
            y += 72f;
            GUI.Label(new Rect(x, y, iw, 40f), "<b>何歳からでも、新しい未来へ挑戦できる人生アップデート空間</b>", sH2);
            y += 50f;
            GUI.Label(new Rect(x, y, iw, 120f),
                MiraiData.Year + "年（設定）。人は長い人生のなかで、仕事や趣味や役割を何度も変えながら学び直します。" +
                "ミライフは、年齢に関係なく、知っていることを教え、だれかの教材から学び、次の学びに気づける場所です。" +
                "あなたも、分野によって先生にも生徒にもなれます。", sBody);
            y += 120f;

            float bw = (iw - 3f * 14f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var f = (MiraiFeature)i;
                var br = new Rect(x + i * (bw + 14f), y, bw, 112f);
                GUI.Box(br, "", sCard);
                GUI.DrawTexture(new Rect(br.x, br.y, br.width, 10f), Solid(MiraiData.FeatureColor(f)));
                GUI.Label(new Rect(br.x + 16f, br.y + 18f, br.width - 32f, 36f), $"<b><color={Hex(MiraiData.FeatureColor(f))}>{MiraiData.FeatureName(f)}</color></b>", sH2);
                GUI.Label(new Rect(br.x + 16f, br.y + 56f, br.width - 32f, 52f), MiraiData.FeatureCaption(f), sSmall);
            }
            y += 132f;

            bool importedAvatars = player != null && player.body != null && player.body.GetComponent<ImportedAvatar>() != null;
            GUI.Label(new Rect(x, y, iw, 36f), importedAvatars
                ? "<b>あなたのアバターをえらべます</b>（あとで変えられます・えらばなくても大丈夫です）"
                : "<b>あなたの服の色をえらべます</b>（あとで変えられます・えらばなくても大丈夫です）", sBody);
            y += 44f;
            int selectedAvatar = importedAvatars ? player.body.GetComponent<ImportedAvatar>().SelectedVariant : avatarChoice;
            for (int i = 0; webSession == null && i < AvatarColors.Length; i++)

            {
                var sr = new Rect(x + i * 100f, y, 84f, 64f);
                if (Btn(sr, i == selectedAvatar ? "✓" : importedAvatars ? (i + 1).ToString() : "", AvatarColors[i])) { avatarChoice = i; ApplyAvatarColor(true); }
            }
            if (webSession != null)
            {
                GUI.DrawTexture(new Rect(x, y, 64f, 64f), Solid(me.color));
                GUI.Label(new Rect(x + 84f, y + 8f, iw - 84f, 56f), webSession.authenticated
                    ? "登録したアバターをアイコンと服の色に反映しています。"
                    : "見学中の操作やお試しデータは保存されません。", sSmall);
            }
            y += 84f;
            GUI.Label(new Rect(x, y, iw - 380f, 100f),
                "マウスを動かすだけで見回せます（ボタンは不要）。W A S D で歩き、1〜6 キーで下のメニューを開けます。\n" +
                "※ 教材・予約・AI・通話は体験用のデモです。操作した内容は保存されません。", sSmall);
            if (Btn(new Rect(r.xMax - 46f - 340f, r.yMax - 34f - 84f, 340f, 84f), "<size=28>3D広場へ参加する</size>", new Color(0.18f, 0.6f, 0.36f)))
                StartPlaza();
        }

        // ================================================================== 学ぶ
        void DrawLearn()
        {
            Color acc = MiraiData.FeatureColor(MiraiFeature.Learn);
            var c = Frame("学ぶ", acc, 1240f, 860f);
            float tw = 260f;
            if (Tab(new Rect(c.x, c.y, tw, 60f), "教材", learnTab == LearnTab.Courses, acc)) { learnTab = LearnTab.Courses; scroll = Vector2.zero; }
            if (Tab(new Rect(c.x + tw + 12f, c.y, tw, 60f), "講座の日程", learnTab == LearnTab.Sessions, acc)) { learnTab = LearnTab.Sessions; scroll = Vector2.zero; }
            if (Tab(new Rect(c.x + 2f * (tw + 12f), c.y, tw, 60f), $"あとで学ぶ（{later.Count}）", learnTab == LearnTab.Later, acc)) { learnTab = LearnTab.Later; scroll = Vector2.zero; }
            var body = new Rect(c.x, c.y + 78f, c.width, c.height - 78f);
            if (learnTab == LearnTab.Courses) DrawCourseList(body, acc);
            else if (learnTab == LearnTab.Sessions) DrawSessionList(body, acc);
            else DrawLaterList(body);
        }

        void DrawCourseList(Rect body, Color acc)
        {
            var cats = new List<string> { "すべて" };
            cats.AddRange(MiraiData.Categories);
            float cw = 176f;
            for (int i = 0; i < cats.Count; i++)
                if (Btn(new Rect(body.x + i * (cw + 10f), body.y, cw, 52f), cats[i], catFilter == cats[i] ? acc * 0.85f : (Color?)null))
                { catFilter = cats[i]; scroll = Vector2.zero; }

            var list = courses.FindAll(x => x.published && (catFilter == "すべて" || x.category == catFilter));
            var area = new Rect(body.x, body.y + 70f, body.width, body.height - 70f);
            if (list.Count == 0) { GUI.Label(area, "この分類の教材はまだありません。", sBody); return; }
            const float rowH = 104f;
            scroll = GUI.BeginScrollView(area, scroll, new Rect(0f, 0f, area.width - 24f, list.Count * (rowH + 10f)));
            float y = 0f;
            foreach (var co in list)
            {
                var rr = new Rect(0f, y, area.width - 30f, rowH);
                if (GUI.Button(rr, "", sCard)) OpenCourse(co, Panel.Learn);
                GUI.Label(new Rect(20f, y + 12f, rr.width - 260f, 36f), $"<b>{co.title}</b>", sH2);
                GUI.Label(new Rect(20f, y + 56f, rr.width - 260f, 36f), $"{co.KindLabel}・{co.minutes}分　｜　{co.author}さん（{co.authorAge}歳）　｜　{co.category}", sSmall);
                string badge = completed.ContainsKey(co.id) ? "<color=#2E7D4F><b>✓ 受講済み</b></color>" : later.Contains(co.id) ? "<color=#B26A00>あとで学ぶ</color>" : "";
                GUI.Label(new Rect(rr.width - 230f, y + 34f, 210f, 36f), badge, sBody);
                y += rowH + 10f;
            }
            GUI.EndScrollView();
        }

        void DrawSessionList(Rect body, Color acc)
        {
            var now = MiraiClock.Now;
            GUI.Label(new Rect(body.x, body.y, body.width, 34f), $"予約制のオンライン講座です。<b>予約の締切は、どの講座も開始30分前</b>です。動画・資料の教材は予約なしで見られます。　いまの時刻：<b>{now.Hour:00}:{now.Minute:00}</b>", sSmall);
            var area = new Rect(body.x, body.y + 52f, body.width, body.height - 52f);
            var list = new List<LectureSession>(sessions);
            list.Sort((a, b) => a.startsAt.CompareTo(b.startsAt));
            const float rowH = 150f;
            scroll = GUI.BeginScrollView(area, scroll, new Rect(0f, 0f, area.width - 24f, list.Count * (rowH + 10f)));
            float y = 0f;
            foreach (var s in list)
            {
                var rr = new Rect(0f, y, area.width - 30f, rowH);
                GUI.Box(rr, "", sCard);
                string status = s.Status(now);
                Color sc = status == "予約できます" ? new Color(0.18f, 0.5f, 0.85f) : status == "予約済み" ? new Color(0.2f, 0.55f, 0.32f) : Muted;
                GUI.Label(new Rect(20f, y + 12f, rr.width - 300f, 36f), $"<b>{s.title}</b>", sH2);
                GUI.Label(new Rect(20f, y + 52f, rr.width - 300f, 32f), $"講師：{s.teacher}さん（{s.teacherAge}歳）　会場：{s.room}", sSmall);
                GUI.Label(new Rect(20f, y + 84f, rr.width - 300f, 32f),
                    $"<b>{MiraiData.FormatDate(s.startsAt)}</b> から{s.minutes}分　｜　予約締切：<b>{s.ClosesAt.Hour:00}:{s.ClosesAt.Minute:00}</b>（開始30分前）", sBody);
                GUI.Label(new Rect(20f, y + 116f, rr.width - 300f, 30f), $"残り {s.SeatsLeft}席／定員{s.capacity}人", sSmall);
                GUI.Label(new Rect(rr.width - 270f, y + 16f, 250f, 34f), $"<b><color={Hex(sc)}>{status}</color></b>", sBody);
                double leftMin = (s.ClosesAt - now).TotalMinutes;
                if (!s.canceled && !s.mine && leftMin > 0 && leftMin < 60)
                    GUI.Label(new Rect(rr.width - 270f, y + 44f, 250f, 26f), $"<color=#B5651D>締切まであと{System.Math.Ceiling(leftMin):0}分</color>", sSmall);
                if (s.myBooking)
                {
                    if (Btn(new Rect(rr.width - 270f, y + 70f, 250f, 60f), "予約を取り消す")) CancelBooking(s);
                }
                else if (!s.mine)
                {
                    bool can = s.CanBook(now);
                    if (Btn(new Rect(rr.width - 270f, y + 70f, 250f, 60f), can ? "予約する" : "予約できません", can ? acc : (Color?)null, can)) Book(s);
                }
                y += rowH + 10f;
            }
            GUI.EndScrollView();
        }

        void DrawLaterList(Rect body)
        {
            var list = courses.FindAll(x => later.Contains(x.id) && x.published);
            if (list.Count == 0) { GUI.Label(body, "「あとで学ぶ」に保存した教材はまだありません。教材の画面から保存できます。", sBody); return; }
            float y = body.y;
            foreach (var co in list)
            {
                var rr = new Rect(body.x, y, body.width, 84f);
                if (GUI.Button(rr, "", sCard)) OpenCourse(co, Panel.Learn);
                GUI.Label(new Rect(rr.x + 20f, y + 12f, rr.width - 40f, 36f), $"<b>{co.title}</b>", sH2);
                GUI.Label(new Rect(rr.x + 20f, y + 48f, rr.width - 40f, 30f), $"{co.KindLabel}・{co.minutes}分　｜　{co.author}さん（{co.authorAge}歳）", sSmall);
                y += 94f;
                if (y > body.yMax - 80f) break;
            }
        }

        void DrawCourseDetail()
        {
            var co = detailCourse;
            if (co == null) { SetPanel(Panel.Learn); return; }
            Color acc = MiraiData.FeatureColor(MiraiFeature.Learn);
            var c = Frame("教材のくわしい内容", acc, 1100f, 760f, "もどる");
            if (!co.published && !co.mine)
            {
                GUI.Label(c, "この教材は現在公開されていません。", sBody);
                return;
            }
            float y = c.y;
            GUI.Label(new Rect(c.x, y, c.width, 48f), $"<size=32><b>{co.title}</b></size>", sTitle);
            y += 60f;
            GUI.Label(new Rect(c.x, y, c.width, 34f), $"{co.KindLabel}・{co.minutes}分　｜　つくった人：<b>{co.author}さん（{co.authorAge}歳）</b>　｜　{co.category}", sBody);
            y += 50f;
            GUI.Label(new Rect(c.x, y, c.width, 120f), co.description, sBody);
            y += 110f;
            y = Chips(c.x, y, c.width, co.tags, new Color(0.85f, 0.93f, 1f)) + 20f;
            GUI.Label(new Rect(c.x, y, c.width, 60f), "予約はいりません。いつでも見られます。最後まで見ても自動で「完了」にはならないので、ご自分で「受講完了にする」を押してください。", sSmall);

            float by = c.yMax - 80f;
            if (Btn(new Rect(c.x, by, 300f, 72f), co.kind == CourseKind.Video ? "<size=26>▶ 動画を見る</size>" : "<size=26>資料をひらく</size>", acc)) { viewerProgress = 0f; SetPanel(Panel.Viewer); }
            bool saved = later.Contains(co.id);
            if (Btn(new Rect(c.x + 320f, by, 300f, 72f), saved ? "あとで学ぶ から外す" : "あとで学ぶ に保存"))
            {
                if (saved) later.Remove(co.id); else later.Add(co.id);
                Toast(saved ? "「あとで学ぶ」から外しました。" : "「あとで学ぶ」に保存しました。");
            }
            if (completed.ContainsKey(co.id))
                GUI.Label(new Rect(c.x + 650f, by + 18f, 400f, 40f), $"<color=#2E7D4F><b>✓ 受講済み（{completed[co.id]:M月d日}）</b></color>", sBody);
        }

        void DrawViewer()
        {
            var co = detailCourse;
            if (co == null) { SetPanel(Panel.Learn); return; }
            Color acc = MiraiData.FeatureColor(MiraiFeature.Learn);
            var c = Frame(co.title, acc, 1240f, 860f, "もどる");
            var screen = new Rect(c.x, c.y, c.width, c.height - 190f);
            GUI.DrawTexture(screen, Solid(co.kind == CourseKind.Video ? new Color(0.2f, 0.24f, 0.28f) : Color.white));
            var big = new GUIStyle(sLabelBig);
            big.normal.textColor = co.kind == CourseKind.Video ? Color.white : Ink;
            GUI.Label(new Rect(screen.x, screen.y + screen.height * 0.32f, screen.width, 60f),
                co.kind == CourseKind.Video ? $"▶ {co.title}" : $"資料：{co.title}", big);
            var sub = new GUIStyle(sCenter);
            sub.normal.textColor = co.kind == CourseKind.Video ? new Color(0.85f, 0.88f, 0.9f) : SubInk;
            GUI.Label(new Rect(screen.x + 40f, screen.y + screen.height * 0.32f + 70f, screen.width - 80f, 90f),
                co.description + "\n（デモ表示：本番では投稿された動画・資料がここに表示されます）", sub);
            var bar = new Rect(c.x, screen.yMax + 14f, c.width, 18f);
            GUI.DrawTexture(bar, Solid(new Color(0.88f, 0.88f, 0.88f)));
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * viewerProgress, bar.height), Solid(acc));

            float by = c.yMax - 86f;
            bool done = completed.ContainsKey(co.id);
            if (Btn(new Rect(c.x, by, 360f, 76f), done ? "✓ 受講完了ずみ" : "<size=26>受講完了にする</size>", new Color(0.18f, 0.6f, 0.36f), !done)) Complete(co);
            GUI.Label(new Rect(c.x + 380f, by + 6f, c.width - 380f, 70f), "見終わったら、ご自分で「受講完了にする」を押してください。押すまで完了にはなりません（何度でも見直せます）。", sSmall);
        }

        // ================================================================== 教える
        void DrawTeach()
        {
            Color acc = MiraiData.FeatureColor(MiraiFeature.Teach);
            var c = Frame("教える", acc, 1240f, 860f);
            if (Tab(new Rect(c.x, c.y, 280f, 60f), "自分の教材", teachTab == TeachTab.Materials, acc)) { teachTab = TeachTab.Materials; scroll = Vector2.zero; }
            if (Tab(new Rect(c.x + 292f, c.y, 280f, 60f), "講座をひらく", teachTab == TeachTab.Lectures, acc)) { teachTab = TeachTab.Lectures; scroll = Vector2.zero; }
            var body = new Rect(c.x, c.y + 80f, c.width, c.height - 80f);
            if (teachTab == TeachTab.Materials) DrawMyMaterials(body, acc);
            else DrawMyLectures(body, acc);
        }

        void DrawMyMaterials(Rect body, Color acc)
        {
            if (Btn(new Rect(body.x, body.y, 380f, 64f), "<size=24>＋ 新しい教材をつくる</size>", acc)) StartEdit(null);
            GUI.Label(new Rect(body.x + 400f, body.y + 14f, body.width - 400f, 40f), "動画や資料のファイルを送って、みんなに公開できます。", sSmall);
            var mine = courses.FindAll(x => x.mine);
            float y = body.y + 84f;
            foreach (var co in mine)
            {
                var rr = new Rect(body.x, y, body.width, 112f);
                GUI.Box(rr, "", sCard);
                string state = co.published ? "<color=#2E7D4F><b>公開中</b></color>" : "<color=#8A6D00><b>下書き</b></color>";
                GUI.Label(new Rect(rr.x + 20f, y + 12f, rr.width - 600f, 36f), $"<b>{co.title}</b>", sH2);
                GUI.Label(new Rect(rr.x + 20f, y + 58f, rr.width - 600f, 36f), $"{state}　｜　{co.KindLabel}　｜　ファイル：{co.fileState}", sSmall);
                float bx = rr.xMax - 570f;
                if (Btn(new Rect(bx, y + 26f, 170f, 60f), "編集")) StartEdit(co);
                if (co.published)
                {
                    if (Btn(new Rect(bx + 182f, y + 26f, 190f, 60f), "非公開にする")) { co.published = false; Toast("非公開にしました。ほかの人の一覧から外れます。"); }
                }
                else if (Btn(new Rect(bx + 182f, y + 26f, 190f, 60f), "公開する", acc, co.fileState == "利用可能"))
                {
                    co.published = true;
                    Toast($"「{co.title}」を公開しました。");
                }
                bool confirm = deleteConfirmId == co.id;
                if (Btn(new Rect(bx + 384f, y + 26f, 170f, 60f), confirm ? "本当に削除" : "削除", confirm ? new Color(0.8f, 0.25f, 0.25f) : (Color?)null))
                {
                    if (confirm) { courses.Remove(co); later.Remove(co.id); deleteConfirmId = null; Toast("教材を削除しました。"); break; }
                    deleteConfirmId = co.id;
                }
                y += 124f;
            }
            if (mine.Count == 0) GUI.Label(new Rect(body.x, y, body.width, 40f), "まだ教材はありません。", sBody);
        }

        void DrawEditCourse()
        {
            Color acc = MiraiData.FeatureColor(MiraiFeature.Teach);
            var c = Frame(editTarget == null ? "新しい教材" : "教材を編集", acc, 1160f, 880f, "もどる");
            float y = c.y;
            GUI.Label(new Rect(c.x, y, 200f, 40f), "<b>題名</b>", sBody);
            editTitle = GUI.TextField(new Rect(c.x + 200f, y - 4f, c.width - 200f, 52f), editTitle, 100, sInput);
            y += 66f;
            GUI.Label(new Rect(c.x, y, 200f, 40f), "<b>説明</b>", sBody);
            editDesc = GUI.TextArea(new Rect(c.x + 200f, y - 4f, c.width - 200f, 130f), editDesc, 2000, sInput);
            y += 146f;
            GUI.Label(new Rect(c.x, y + 6f, 200f, 40f), "<b>分類</b>", sBody);
            for (int i = 0; i < MiraiData.Categories.Length; i++)
            {
                string cat = MiraiData.Categories[i];
                if (Btn(new Rect(c.x + 200f + i * 200f, y, 188f, 56f), cat, editCat == cat ? acc : (Color?)null)) editCat = cat;
            }
            y += 74f;
            GUI.Label(new Rect(c.x, y + 6f, 200f, 40f), "<b>種類</b>", sBody);
            if (Btn(new Rect(c.x + 200f, y, 188f, 56f), "動画", editKind == CourseKind.Video ? acc : (Color?)null)) editKind = CourseKind.Video;
            if (Btn(new Rect(c.x + 400f, y, 188f, 56f), "資料（PDF）", editKind == CourseKind.Document ? acc : (Color?)null)) editKind = CourseKind.Document;
            y += 74f;
            GUI.Label(new Rect(c.x, y + 6f, 200f, 40f), "<b>ファイル</b>", sBody);
            bool busy = uploadStart > 0f;
            if (Btn(new Rect(c.x + 200f, y, 300f, 56f), busy ? "確認中…" : "ファイルを選ぶ（デモ）", null, !busy)) { uploadStart = Time.time; editFileState = "送信中"; }
            Color fs = editFileState == "利用可能" ? new Color(0.18f, 0.55f, 0.3f) : SubInk;
            GUI.Label(new Rect(c.x + 520f, y + 8f, c.width - 520f, 40f), $"状態：<b><color={Hex(fs)}>{editFileState}</color></b>", sBody);
            y += 70f;
            GUI.Label(new Rect(c.x + 200f, y, c.width - 200f, 60f), "本番ではファイルを直接送り、サーバーで中身を確かめてから「利用可能」になります。", sSmall);

            string prob = EditProblem();
            float by = c.yMax - 80f;
            if (prob != null) GUI.Label(new Rect(c.x, by - 50f, c.width, 40f), $"<color=#B23A3A>{prob}</color>", sBody);
            if (Btn(new Rect(c.x, by, 300f, 72f), "下書き保存", null, prob == null)) SaveEdit(false);
            if (Btn(new Rect(c.x + 320f, by, 300f, 72f), "<size=26>公開する</size>", acc, prob == null && editFileState == "利用可能")) SaveEdit(true);
        }

        void DrawMyLectures(Rect body, Color acc)
        {
            var now = MiraiClock.Now;
            float y = body.y;
            GUI.Label(new Rect(body.x, y, body.width, 36f), "<b>新しい講座の募集</b>", sH2);
            y += 46f;
            GUI.Label(new Rect(body.x, y + 8f, 140f, 40f), "講座名", sBody);
            lecTitle = GUI.TextField(new Rect(body.x + 140f, y, 620f, 52f), lecTitle, 100, sInput);
            y += 66f;
            GUI.Label(new Rect(body.x, y + 8f, 140f, 40f), "日にち", sBody);
            string[] days = { "明日", "あさって", "しあさって" };
            for (int i = 0; i < 3; i++)
                if (Btn(new Rect(body.x + 140f + i * 172f, y, 160f, 54f), days[i], lecDay == i + 1 ? acc : (Color?)null)) lecDay = i + 1;
            y += 66f;
            GUI.Label(new Rect(body.x, y + 8f, 140f, 40f), "時刻", sBody);
            for (int i = 0; i < LecTimes.Length; i++)
                if (Btn(new Rect(body.x + 140f + i * 172f, y, 160f, 54f), LecTimes[i], lecTime == i ? acc : (Color?)null)) lecTime = i;
            y += 66f;
            GUI.Label(new Rect(body.x, y + 8f, 140f, 40f), "定員", sBody);
            if (Btn(new Rect(body.x + 140f, y, 64f, 54f), "－", null, lecCap > 1)) lecCap--;
            GUI.Label(new Rect(body.x + 210f, y + 8f, 110f, 40f), $"<b>{lecCap}人</b>", sCenter);
            if (Btn(new Rect(body.x + 326f, y, 64f, 54f), "＋", null, lecCap < 7)) lecCap++;
            GUI.Label(new Rect(body.x + 420f, y + 8f, body.width - 420f, 40f), "予約の締切は<b>開始30分前</b>で決まっています（変更できません）。", sSmall);
            y += 70f;
            bool ok = lecTitle.Trim().Length > 0;
            if (Btn(new Rect(body.x, y, 320f, 66f), "<size=24>募集を公開する</size>", acc, ok))
            {
                string[] hm = LecTimes[lecTime].Split(':');
                var start = now.Date.AddDays(lecDay).AddHours(int.Parse(hm[0])).AddMinutes(int.Parse(hm[1]));
                var ls = new LectureSession
                {
                    id = "my" + sessions.Count, title = lecTitle.Trim(), teacher = me.name, teacherAge = me.age,
                    startsAt = start, capacity = lecCap, booked = 0, mine = true, room = "学びの部屋", description = ""
                };
                sessions.Add(ls);
                lecTitle = "";
                Toast($"募集を公開しました。{MiraiData.FormatDate(start)}開始、予約締切は{ls.ClosesAt.Hour:00}:{ls.ClosesAt.Minute:00}です。");
            }
            if (!ok) GUI.Label(new Rect(body.x + 340f, y + 14f, 500f, 40f), "講座名を入れてください。", sSmall);
            y += 90f;

            GUI.Label(new Rect(body.x, y, body.width, 36f), "<b>あなたが講師の講座</b>", sH2);
            y += 44f;
            var mine = sessions.FindAll(x => x.mine);
            if (mine.Count == 0) GUI.Label(new Rect(body.x, y, body.width, 36f), "まだありません。", sBody);
            foreach (var s in mine)
            {
                if (y > body.yMax - 70f) break;
                var rr = new Rect(body.x, y, body.width, 70f);
                GUI.Box(rr, "", sCard);
                GUI.Label(new Rect(rr.x + 20f, y + 16f, rr.width - 300f, 40f),
                    $"<b>{s.title}</b>　{MiraiData.FormatDate(s.startsAt)}　予約 {s.booked}/{s.capacity}人　{(s.canceled ? "<color=#B23A3A>中止</color>" : "")}", sBody);
                if (!s.canceled && Btn(new Rect(rr.xMax - 230f, y + 8f, 210f, 54f), "中止する")) { s.canceled = true; Toast("講座を中止しました（本番では予約者に知らせます）。"); }
                y += 80f;
            }
        }

        // ================================================================== つながる
        void DrawConnect()
        {
            Color acc = MiraiData.FeatureColor(MiraiFeature.Connect);
            var c = Frame("つながる", acc, 1180f, 860f);
            GUI.Label(new Rect(c.x, c.y, c.width, 60f), "4つの交流室で話せます（各部屋8人まで）。入室しても<b>マイク・カメラはOFF</b>のままで、ご自分でONにできます。ロビーには通話はありません。", sSmall);
            float y = c.y + 70f;
            foreach (var r in campus.Rooms)
            {
                var rr = new Rect(c.x, y, c.width, 112f);
                GUI.Box(rr, "", sCard);
                GUI.DrawTexture(new Rect(rr.x, rr.y, 12f, rr.height), Solid(r.color));
                GUI.Label(new Rect(rr.x + 30f, y + 12f, rr.width - 640f, 40f), $"<b>{r.name}</b>　<size=20>{RoomCount(r)}/8人</size>", sH2);
                GUI.Label(new Rect(rr.x + 30f, y + 56f, rr.width - 640f, 40f), r.caption, sSmall);
                if (Btn(new Rect(rr.xMax - 600f, y + 24f, 280f, 64f), "歩いて行く（案内）"))
                {
                    SetPanel(Panel.None);
                    Guide(r.door, r.name);
                }
                if (Btn(new Rect(rr.xMax - 304f, y + 24f, 284f, 64f), "この部屋に入る", r.color * 0.85f))
                {
                    SetPanel(Panel.None);
                    Vector3 d = r.center - r.door; d.y = 0f;
                    player.Teleport(r.center + Vector3.up * 0.3f - d.normalized * 3f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                    EnterRoom(r);
                }
                y += 124f;
            }

            // Could：教えられる人・学びたい人の候補（デモ）
            y += 6f;
            GUI.Label(new Rect(c.x, y, c.width, 36f), "<b>あなたと話が合いそうな人</b>　<size=18><color=#888888>（デモ人物・試作機能）</color></size>", sH2);
            y += 46f;
            float x = c.x;
            foreach (var p in people)
            {
                string why = null;
                foreach (var t in p.teaches) if (me.learns.Contains(t)) { why = $"「{t}」を教えられます"; break; }
                if (why == null) foreach (var l in p.learns) if (me.teaches.Contains(l)) { why = $"「{l}」を学びたい"; break; }
                if (why == null) continue;
                if (x + 360f > c.xMax) { x = c.x; y += 74f; }
                if (y > c.yMax - 64f) break;
                if (Btn(new Rect(x, y, 350f, 64f), $"{p.name}さん（{p.age}歳）<size=17>\n{why}</size>")) { personView = p; SetPanel(Panel.Person); }
                x += 362f;
            }
        }

        // ================================================================== 未来ナビ
        void DrawNavi()
        {
            Color acc = MiraiData.FeatureColor(MiraiFeature.Navi);
            var c = Frame("未来ナビ", acc, 1200f, 880f);
            float y = c.y;
            GUI.Label(new Rect(c.x, y, c.width, 36f), "<b>使う情報</b>（ご本人のプロフィールと受講履歴だけを使います）", sBody);
            y += 44f;
            var info = new Rect(c.x, y, c.width, 132f);
            GUI.Box(info, "", sCard);
            GUI.Label(new Rect(info.x + 20f, y + 10f, info.width - 300f, 34f), $"学びたいこと：<b>{Join(me.learns)}</b>", sBody);
            GUI.Label(new Rect(info.x + 20f, y + 46f, info.width - 300f, 34f), $"教えられること：{Join(me.teaches)}", sSmall);
            GUI.Label(new Rect(info.x + 20f, y + 82f, info.width - 300f, 34f),
                webSession != null ? $"お試しの受講完了：{completed.Count}件（保存なし）｜ 年齢は利用しません" :
                $"受講した教材：{completed.Count}件　｜　年齢（{me.age}歳）の利用：{(me.aiUsesAge ? "する" : "しない")}", sSmall);
            if (Btn(new Rect(info.xMax - 270f, y + 34f, 250f, 64f), webSession != null ? "プロフィールを見る" : "プロフィールを直す")) SetPanel(Panel.Profile);
            y += 152f;

            bool waiting = naviReadyAt > 0f;
            if (Btn(new Rect(c.x, y, 380f, 76f), waiting ? "考えています…" : naviResult == null ? "<size=26>提案してもらう</size>" : "<size=24>もう一度提案してもらう</size>", acc, !waiting))
            {
                naviResult = null;
                naviReadyAt = Time.time + 1.2f;
            }
            GUI.Label(new Rect(c.x + 400f, y + 6f, c.width - 400f, 70f), "ボタンを押したときだけ提案します。最大3件の中から、学びたいものを選んでください。選んだだけでは予約や完了にはなりません。", sSmall);
            y += 96f;

            if (naviResult != null)
            {
                if (naviResult.suggestions.Count == 0)
                    GUI.Label(new Rect(c.x, y, c.width, 80f), naviResult.emptyReason, sBody);
                foreach (var s in naviResult.suggestions)
                {
                    var rr = new Rect(c.x, y, c.width, 136f);
                    if (rr.yMax > c.yMax - 36f) break;
                    GUI.Box(rr, "", sCard);
                    GUI.Label(new Rect(rr.x + 20f, y + 10f, rr.width - 320f, 36f), $"<b>{s.course.title}</b>　<size=18>{s.course.KindLabel}・{s.course.minutes}分｜{s.course.author}さん（{s.course.authorAge}歳）</size>", sH2);
                    GUI.Label(new Rect(rr.x + 20f, y + 52f, rr.width - 320f, 36f), $"理由：{s.reason}", sSmall);
                    if (!string.IsNullOrEmpty(s.nextLearning))
                        GUI.Label(new Rect(rr.x + 20f, y + 88f, rr.width - 320f, 36f), $"<color=#2E7D4F>{s.nextLearning}</color>", sSmall);
                    if (Btn(new Rect(rr.xMax - 290f, y + 34f, 270f, 68f), "この教材を見る", acc)) OpenCourse(s.course, Panel.Navi);
                    y += 148f;
                }
            }
            GUI.Label(new Rect(c.x, c.yMax - 30f, c.width, 30f), "※ このデモの提案はルールで作っています。本番ではサーバー経由の生成AIが、同じ画面に提案を返します。", sSmall);
        }

        static string Join(List<string> l) => l.Count == 0 ? "（まだありません）" : string.Join("・", l.ConvertAll(Plain).ToArray());

        static string Plain(string text) => (text ?? "").Replace("<", "＜").Replace(">", "＞");

        void DrawWebProfile()
        {
            Color acc = new Color(0.35f, 0.45f, 0.6f);
            var c = Frame("わたしのプロフィール", acc, 1120f, 720f);
            bool authenticated = webSession.authenticated;
            float width = c.width - 30f;
            string teaches = "<b>教えられること</b>\n" + Join(me.teaches);
            string learns = "<b>学びたいこと</b>\n" + Join(me.learns);
            float teachesHeight = Mathf.Max(96f, sBody.CalcHeight(new GUIContent(teaches), width));
            float learnsHeight = Mathf.Max(96f, sBody.CalcHeight(new GUIContent(learns), width));
            var view = new Rect(c.x, c.y, c.width, c.height - 90f);
            Track(view);
            webProfileScroll = GUI.BeginScrollView(view, webProfileScroll, new Rect(0f, 0f, width, 240f + teachesHeight + learnsHeight));
            float y = 0f;
            GUI.Label(new Rect(0f, y, width, 52f), $"<b>{Plain(me.name)}{(authenticated ? "さん" : "で見学中")}</b>", sTitle);
            y += 68f;
            GUI.Label(new Rect(0f, y, width, 48f), authenticated
                ? $"アバター：{WebSessionBridge.AvatarLabel(webSession.profile.avatarId)}（人型の服の色に反映）"
                : "ログインすると、登録したプロフィールで参加できます。", sBody);
            y += 68f;
            GUI.Label(new Rect(0f, y, width, teachesHeight), teaches, sBody);
            y += teachesHeight + 16f;
            GUI.Label(new Rect(0f, y, width, learnsHeight), learns, sBody);
            y += learnsHeight + 16f;
            GUI.Label(new Rect(0f, y, width, 64f), "登録したプロフィールを表示しています。教材・予約のお試しデータは保存されません。", sSmall);
            GUI.EndScrollView();
            if (Btn(new Rect(c.x, c.yMax - 70f, 330f, 64f), authenticated ? "マイページをひらく" : "ログインする", acc))
                WebSessionBridge.OpenAccount(authenticated);
        }

        // ================================================================== プロフィール
        void DrawProfile()
        {
            if (webSession != null) { DrawWebProfile(); return; }
            Color acc = new Color(0.35f, 0.45f, 0.6f);
            var c = Frame("わたしのプロフィール", acc, 1240f, 900f);
            float viewH = 2900f + me.quals.Count * 72f + completed.Count * 40f;
            scroll = GUI.BeginScrollView(c, scroll, new Rect(0f, 0f, c.width - 24f, viewH));
            float x = 0f, y = 0f, iw = c.width - 40f;

            GUI.Label(new Rect(x, y + 8f, 200f, 40f), "<b>表示名</b>", sBody);
            me.name = GUI.TextField(new Rect(x + 200f, y, 400f, 52f), me.name, 20, sInput);
            y += 70f;
            GUI.Label(new Rect(x, y + 8f, 200f, 40f), "<b>年齢</b>", sBody);
            if (Btn(new Rect(x + 200f, y, 64f, 56f), "－", null, me.age > 0)) me.age--;
            GUI.Label(new Rect(x + 270f, y + 8f, 120f, 40f), $"<b>{me.age}歳</b>", sCenter);
            if (Btn(new Rect(x + 396f, y, 64f, 56f), "＋", null, me.age < 130)) me.age++;
            GUI.Label(new Rect(x + 480f, y + 4f, iw - 480f, 60f), $"ほかの利用者のプロフィールに「{me.age}歳」と表示されます。", sSmall);
            y += 76f;

            y = DrawWardrobe(x, y, iw);

            y = SkillPicker(x, y, iw, "教えられること", me.teaches, me.learns, MiraiData.FeatureColor(MiraiFeature.Teach)) + 18f;
            y = SkillPicker(x, y, iw, "学びたいこと", me.learns, me.teaches, MiraiData.FeatureColor(MiraiFeature.Learn)) + 24f;

            // 資格
            GUI.Label(new Rect(x, y, iw, 40f), "<b>資格</b>　<size=18>（自己申告です。なくても使えます）</size>", sH2);
            y += 48f;
            for (int i = 0; i < me.quals.Count; i++)
            {
                var q = me.quals[i];
                var rr = new Rect(x, y, iw, 62f);
                GUI.Box(rr, "", sCard);
                string extra = (string.IsNullOrEmpty(q.issuer) ? "" : $"　{q.issuer}") + (string.IsNullOrEmpty(q.acquired) ? "" : $"　{q.acquired}");
                GUI.Label(new Rect(rr.x + 20f, y + 12f, iw - 220f, 40f), $"<b>{q.name}</b><size=18>{extra}</size>", sBody);
                if (Btn(new Rect(rr.xMax - 170f, y + 6f, 150f, 50f), "削除")) { me.quals.RemoveAt(i); break; }
                y += 72f;
            }
            GUI.Label(new Rect(x, y + 8f, 150f, 40f), "資格名", sSmall);
            qName = GUI.TextField(new Rect(x + 120f, y, 330f, 50f), qName, 40, sInput);
            GUI.Label(new Rect(x + 470f, y + 8f, 150f, 40f), "発行団体", sSmall);
            qIssuer = GUI.TextField(new Rect(x + 570f, y, 240f, 50f), qIssuer, 40, sInput);
            GUI.Label(new Rect(x + 830f, y + 8f, 150f, 40f), "取得年月", sSmall);
            qWhen = GUI.TextField(new Rect(x + 930f, y, 200f, 50f), qWhen, 20, sInput);
            y += 62f;
            if (Btn(new Rect(x, y, 260f, 58f), "＋ 資格を追加", acc, qName.Trim().Length > 0))
            {
                me.quals.Add(new Qualification { name = qName.Trim(), issuer = qIssuer.Trim(), acquired = qWhen.Trim() });
                qName = qIssuer = qWhen = "";
                Toast("資格を追加しました。");
            }
            y += 84f;

            // AI利用設定（年齢の表示とは別）
            GUI.Label(new Rect(x, y, iw, 40f), "<b>未来ナビの設定</b>", sH2);
            y += 48f;
            if (Btn(new Rect(x, y, 420f, 58f), me.aiUsesAge ? "✓ 提案に年齢を使う" : "提案に年齢を使わない", me.aiUsesAge ? acc : (Color?)null)) me.aiUsesAge = !me.aiUsesAge;
            GUI.Label(new Rect(x + 440f, y + 8f, iw - 440f, 50f), "OFFにしても、プロフィールの年齢表示は消えません。", sSmall);
            y += 84f;

            // 受講履歴・予約（本人だけが見られる）
            GUI.Label(new Rect(x, y, iw, 40f), "<b>受講した教材</b>　<size=18>（あなただけが見られます）</size>", sH2);
            y += 46f;
            if (completed.Count == 0) { GUI.Label(new Rect(x, y, iw, 36f), "まだありません。", sSmall); y += 40f; }
            foreach (var kv in completed)
            {
                var co = courses.Find(cc => cc.id == kv.Key);
                GUI.Label(new Rect(x, y, iw, 36f), $"・{(co != null ? co.title : kv.Key)}　<size=18>{kv.Value:M月d日 HH:mm}</size>", sBody);
                y += 40f;
            }
            y += 20f;
            GUI.Label(new Rect(x, y, iw, 40f), "<b>予約している講座</b>", sH2);
            y += 46f;
            var booked = sessions.FindAll(s => s.myBooking);
            if (booked.Count == 0) { GUI.Label(new Rect(x, y, iw, 36f), "まだありません。", sSmall); y += 40f; }
            foreach (var s in booked)
            {
                GUI.Label(new Rect(x, y, iw, 36f), $"・{s.title}　{MiraiData.FormatDate(s.startsAt)}（{s.room}）", sBody);
                y += 40f;
            }
            GUI.EndScrollView();
        }

        float SkillPicker(float x, float y, float maxW, string label, List<string> mine, List<string> other, Color on)
        {
            GUI.Label(new Rect(x, y, maxW, 40f), $"<b>{label}</b>　<size=18>（押すと切り替わります）</size>", sH2);
            y += 48f;
            float cx = x, cy = y, rowH = 0f;
            foreach (var sk in MiraiData.SkillCatalog)
            {
                bool sel = mine.Contains(sk);
                string text = sel ? $"✓ {sk}" : sk;
                float w = sChip.CalcSize(new GUIContent(text)).x + 30f;
                if (cx + w > x + maxW && cx > x) { cx = x; cy += rowH + 10f; rowH = 0f; }
                if (Btn(new Rect(cx, cy, w, 54f), text, sel ? on : (Color?)null))
                {
                    if (sel) mine.Remove(sk);
                    else { mine.Add(sk); other.Remove(sk); }
                }
                cx += w + 10f;
                rowH = 54f;
            }
            return cy + rowH;
        }

        // ================================================================== 人のプロフィール（公開項目だけ）
        void DrawPerson()
        {
            var p = personView;
            if (p == null) { SetPanel(Panel.None); return; }
            Color acc = new Color(0.35f, 0.45f, 0.6f);
            var c = Frame($"{p.name}さんのプロフィール", acc, 1100f, 860f);
            float y = c.y;
            GUI.Label(new Rect(c.x, y, c.width, 50f), $"<size=32><b>{p.name}さん</b></size>　<size=26>{p.age}歳</size>　<size=18><color=#888888>デモ人物（実際の参加者ではありません）</color></size>", sTitle);
            y += 60f;
            GUI.Label(new Rect(c.x, y, c.width, 44f), p.bio, sBody);
            y += 56f;
            GUI.Label(new Rect(c.x, y, c.width, 36f), "<b>教えられること</b>", sH2);
            y = Chips(c.x, y + 42f, c.width, p.teaches, new Color(1f, 0.9f, 0.78f)) + 16f;
            GUI.Label(new Rect(c.x, y, c.width, 36f), "<b>学びたいこと</b>", sH2);
            y = Chips(c.x, y + 42f, c.width, p.learns, new Color(0.85f, 0.93f, 1f)) + 16f;
            GUI.Label(new Rect(c.x, y, c.width, 36f), "<b>資格</b>　<size=18>（自己申告）</size>", sH2);
            y += 42f;
            if (p.quals.Count == 0) { GUI.Label(new Rect(c.x, y, c.width, 34f), "登録なし", sSmall); y += 36f; }
            foreach (var q in p.quals) { GUI.Label(new Rect(c.x, y, c.width, 34f), "・" + q.name, sBody); y += 36f; }
            y += 12f;
            GUI.Label(new Rect(c.x, y, c.width, 36f), $"<b>{p.name}さんの教材</b>", sH2);
            y += 46f;
            var theirs = courses.FindAll(x => x.published && x.author == p.name);
            if (theirs.Count == 0) GUI.Label(new Rect(c.x, y, c.width, 34f), "公開中の教材はありません。", sSmall);
            foreach (var co in theirs)
            {
                if (y > c.yMax - 70f) break;
                if (Btn(new Rect(c.x, y, c.width, 60f), $"{co.title}　<size=18>（{co.KindLabel}・{co.minutes}分）</size>")) OpenCourse(co, Panel.Person);
                y += 70f;
            }
        }

        // ================================================================== ヘルプ・設定
        void DrawHelp()
        {
            Color acc = new Color(0.35f, 0.45f, 0.6f);
            var c = Frame("使い方と設定", acc, 1100f, 860f);
            float y = c.y;
            string[] lines =
            {
                "<b>まわりを見る</b>：<b>マウスを動かすだけ</b>（ボタンは不要）。ホイールで近づく・離れる。Esc でマウスカーソルを出し、画面をクリックすると戻ります。",
                "<b>歩く</b>：W A S D か 矢印キー（Shift で速く）。画面中央の点で地面を指してクリックすると、そこまで歩きます。",
                "<b>メニュー</b>：<b>1</b> 学ぶ／<b>2</b> 教える／<b>3</b> つながる／<b>4</b> 未来ナビ／<b>5</b> プロフィール／<b>6</b> ヘルプ",
                "<b>視点をもどす</b>：R キー、または下の「視点をもどす」ボタン。",
                "<b>人や入口</b>：近づくと説明が出ます。「ひらく」「入室する」を押したときだけ動きます。",
                "<b>画面を閉じる</b>：右上の「閉じる」か Esc キー。開いている間は歩きません。",
            };
            foreach (var l in lines) { GUI.Label(new Rect(c.x, y, c.width, 64f), l, sBody); y += 66f; }
            y += 16f;

            GUI.Label(new Rect(c.x, y + 10f, 260f, 40f), "<b>文字の大きさ</b>", sBody);
            string[] sizes = { "ふつう", "大きい", "とても大きい" };
            float[] vals = { 1f, 1.15f, 1.35f };
            for (int i = 0; i < 3; i++)
                if (Btn(new Rect(c.x + 260f + i * 230f, y, 218f, 60f), sizes[i], Mathf.Abs(userScale - vals[i]) < 0.01f ? acc : (Color?)null)) userScale = vals[i];
            y += 80f;
            GUI.Label(new Rect(c.x, y + 10f, 260f, 40f), "<b>まわりを見る速さ</b>", sBody);
            if (Btn(new Rect(c.x + 260f, y, 64f, 60f), "－", null, player.lookSensitivity > 0.3f)) player.lookSensitivity = Mathf.Max(0.3f, player.lookSensitivity - 0.2f);
            GUI.Label(new Rect(c.x + 330f, y + 10f, 120f, 40f), $"<b>{player.lookSensitivity:0.0}</b>", sCenter);
            if (Btn(new Rect(c.x + 456f, y, 64f, 60f), "＋", null, player.lookSensitivity < 4f)) player.lookSensitivity = Mathf.Min(4f, player.lookSensitivity + 0.2f);
            y += 80f;
            if (Btn(new Rect(c.x, y, 300f, 64f), "視点をもどす")) { player.ResetView(); SetPanel(Panel.None); }
            if (Btn(new Rect(c.x + 320f, y, 340f, 64f), "はじめの場所にもどる"))
            {
                if (currentRoom != null) LeaveRoom(false);
                player.Teleport(campus.Spawn, campus.SpawnYaw);
                SetPanel(Panel.None);
            }
        }
    }
}
