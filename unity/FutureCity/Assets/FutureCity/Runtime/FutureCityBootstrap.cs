using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// 空のシーンにこのコンポーネントを1つ置くだけで、未来都市「ミライフ（NEO HORIZON 2127）」一式を生成する。
    /// （メニュー Tools > Future City > Create Scene で自動配置されます）
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class FutureCityBootstrap : MonoBehaviour
    {
        [Header("乱数シード（変えると別の街になります）")]
        public int seed = 2126;

        [Header("都市の規模")]
        [Range(6, 20)] public int gridSize = 14;
        public float blockSize = 64f;
        public float roadWidth = 18f;
        public float maxHeight = 460f;
        [Range(0f, 0.3f)] public float parkChance = 0.07f;

        [Header("交通量")]
        [Range(0, 2000)] public int flyingVehicles = 400;
        [Range(0, 1000)] public int groundVehicles = 200;

        [Header("歩行（シニア向けにゆっくりめ）")]
        public float walkSpeed = 3.2f;
        public float runSpeed = 6f;
        [Tooltip("まわりを見る速さ（プレイ中もヘルプ画面で変えられます）")]
        [Range(0.2f, 4f)] public float mouseSensitivity = 1.2f;

        [Header("時刻（夜にはしない）")]
        [Range(7f, 16f)] public float startTime = 10.5f;

        [Header("背景（まわりは実写の360°写真）")]
        [Tooltip("まわりの街を作らず、広場のまわりを実写の写真にする（Resources/FutureCityBackdrop の写真）。オフで従来の未来都市")]
        public bool photoBackdrop = true;
        [Tooltip("写真の明るさの微調整（EV）。+1 で2倍、-1 で半分")]
        [Range(-2f, 2f)] public float backdropExposure = 0f;
        [Tooltip("写真の太陽が広場を斜め後ろから照らす向きに、写真を自動で回す")]
        public bool autoRotateBackdrop = true;
        [Tooltip("自動で回さないときの写真の向き（度）")]
        [Range(0f, 360f)] public float backdropRotation = 0f;

        [Header("品質")]
        [Tooltip("葉や草のふちのギザギザ・ちらつきを消す（TAA）。オフにすると MSAA＋SMAA")]
        public bool temporalAA = true;
        public bool realtimeReflections = true;
        public bool postProcessing = true;
        [Tooltip("広場を歩く人・座る人（背景の人物）を出す")]
        public bool ambientPeople = true;
        [Tooltip("芝生に立体的な草を生やす（パソコンが重いときはオフ）")]
        public bool lawnGrass = true;
        [Tooltip("描画解像度の倍率。1.0=画面と同じ、1.25〜1.5=よりくっきり（重くなる）")]
        [Range(0.75f, 2f)] public float renderScale = 1f;

        [Header("焼き込み済みマテリアル（エディタメニューが自動設定）")]
        public Material[] bakedMaterials;
        public Material bakedSkybox;

        public MaterialLibrary Library { get; private set; }
        public CityGenerator City { get; private set; }
        public PhotoBackdrop Backdrop { get; private set; }

        void Awake()
        {
            Library = MaterialLibrary.Create(bakedMaterials, bakedSkybox, seed);
            RenderSettings.skybox = Library.Skybox;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            bool photo = photoBackdrop;
            // 葉・花・草のカードは、実写テクスチャ（葉の切り抜き画像）があるときだけ使う
            bool cards = photo && Library.HasPhotoTextures;

            // ---- カメラ
            var cam = Camera.main;
            if (cam == null)
            {
                var cg = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cg.AddComponent<Camera>();
                cg.AddComponent<AudioListener>();
            }
            cam.farClipPlane = photo ? 16000f : 9000f;      // 写真モード：地平線まで続く湖が切れないように
            cam.nearClipPlane = photo ? 0.1f : 0.08f;
            cam.fieldOfView = 65f;
            cam.clearFlags = CameraClearFlags.Skybox;

            // ---- 太陽と月
            var sun = FindSun();
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            RenderSettings.sun = sun;

            // ---- 世界（写真モード：広場・公園・湖だけ ／ 都市モード：未来都市まるごと）
            City = new GameObject("City").AddComponent<CityGenerator>();
            City.transform.SetParent(transform, false);
            City.seed = seed;
            City.gridSize = gridSize;
            City.blockSize = blockSize;
            City.roadWidth = roadWidth;
            City.maxHeight = maxHeight;
            City.parkChance = parkChance;
            City.photoMode = photo;
            // 広場の質感：箱の角を少し丸め（やわらかいハイライト）、木と花を葉のカードで作る
            MeshBuilder.BevelSize = photo ? 0.025f : 0f;
            if (cards) Foliage.Begin();
            City.Generate(Library);

            if (!photo)
            {
                var traffic = new GameObject("Traffic").AddComponent<TrafficSystem>();
                traffic.transform.SetParent(transform, false);
                traffic.flyingCount = flyingVehicles;
                traffic.groundCount = groundVehicles;
                traffic.Init(City, Library, seed + 7);

                var maglev = new GameObject("Maglev").AddComponent<MaglevSystem>();
                maglev.transform.SetParent(transform, false);
                maglev.Init(City, Library);
            }

            gameObject.AddComponent<EmissionAnimator>().lib = Library;
            gameObject.AddComponent<WaterAnimator>().materials = new[] { Library.Get(Mat.Water), Library.Get(Mat.Lake) };

            // ---- 背景の写真（太陽・環境光・かすみを写真に合わせる）
            bool photoSky = false;
            if (photo)
            {
                Backdrop = gameObject.AddComponent<PhotoBackdrop>();
                Backdrop.exposureBias = backdropExposure;
                Backdrop.autoRotate = autoRotateBackdrop;
                Backdrop.manualRotation = backdropRotation;
                photoSky = Backdrop.Apply(sun, Library);
            }

            // ---- 反射プローブ（ガラス・水面・金属に、空と広場を映す）
            ReflectionProbe probe = null;
            if (realtimeReflections)
            {
                var pgo = new GameObject("CityReflectionProbe");
                pgo.transform.SetParent(transform, false);
                probe = pgo.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.resolution = 512;
                probe.hdr = true;
                probe.importance = 1;
                if (photo)
                {
                    // 広場の真ん中から一度だけ撮る。湖の水平線まで含めるので、範囲と描画距離を大きく
                    pgo.transform.position = new Vector3(0f, 9f, -28f);   // 中央の彫刻をさけて、南の大通りの上
                    probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                    probe.size = new Vector3(32000f, 6000f, 32000f);
                    probe.boxProjection = false;
                    probe.nearClipPlane = 0.3f;
                    probe.farClipPlane = 16000f;
                    probe.cullingMask = ~(1 << PlayerWalker.PeopleLayer);
                }
                else
                {
                    pgo.transform.position = new Vector3(0f, 60f, 0f);
                    probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                    probe.size = new Vector3(City.HalfExtent * 2f + 600f, 1200f, City.HalfExtent * 2f + 600f);
                    probe.farClipPlane = 4000f;
                }
            }

            // ---- 昼夜（写真モードでは写真の太陽に固定）
            if (!photoSky)
            {
                var moon = new GameObject("Moon").AddComponent<Light>();
                moon.transform.SetParent(transform, false);
                moon.type = LightType.Directional;
                moon.color = new Color(0.55f, 0.65f, 1f);
                moon.shadows = LightShadows.None;
                moon.transform.rotation = Quaternion.Euler(38f, 150f, 0f);

                var dn = gameObject.AddComponent<DayNightCycle>();
                dn.sun = sun;
                dn.moon = moon;
                dn.lib = Library;
                dn.probe = probe;
                dn.timeOfDay = startTime;
                dn.autoAdvance = false;
                dn.Apply();
                DynamicGI.UpdateEnvironment();
            }

            if (postProcessing) PostFXSetup.Apply(cam, transform, renderScale, photo, temporalAA);

            // ---- 歩行プレイヤー（ミライフ庭園キャンパスの南の入口からスタート）
            var campus = City.Campus;
            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            playerGo.AddComponent<CharacterController>();
            var player = playerGo.AddComponent<PlayerWalker>();
            player.cam = cam;
            player.walkSpeed = walkSpeed;
            player.runSpeed = runSpeed;
            player.lookSensitivity = mouseSensitivity;

            // 三人称で表示する標準アバター（服の色は入場画面で選べる）
            var avatar = MiraiAvatar.Build(Library, playerGo.transform, "PlayerAvatar", 1f,
                                           new Color(0.98f, 0.82f, 0.6f), Mat.SignOrange, false, 78);   // はるこさん（78歳）
            player.body = avatar.transform;
            player.Place(campus.Spawn, campus.SpawnYaw);
            cam.transform.SetPositionAndRotation(campus.Spawn + new Vector3(0f, 3f, -5f), Quaternion.Euler(15f, campus.SpawnYaw, 0f));

            // ---- 参考画像の世界観（輪の彫刻・空中回廊・ガラスのドーム・パーゴラ）と、広場のにぎわい
            var park = MiraiPark.Build(Library, City, transform);
            if (cards) Foliage.End(Library, transform);
            MeshBuilder.BevelSize = 0f;
            // 芝生の上だけに立体的な草を生やす（キャンパスの中は密に、まわりの公園は少しまばらに）
            if (cards && lawnGrass) LawnGrass.Populate(Library, transform, campus.Center, campus.Half, 0.6f, City.landRadius - 1f, 0.9f, seed);
            if (ambientPeople) AmbientPeople.Spawn(Library, campus, park, playerGo.transform, transform);

            // ---- ミライフ（3D空間の上のパネル・案内・デモ人物）
            var mirai = new GameObject("MiraiLife").AddComponent<MiraiWorld>();
            mirai.transform.SetParent(transform, false);
            MiraiClock.Start(startTime);   // 太陽と講座の時刻を同じ時計にそろえる
            mirai.Init(Library, player, cam, campus);
        }

        void Start()
        {
            var probe = GetComponentInChildren<ReflectionProbe>();
            if (probe != null) probe.RenderProbe();
        }

        static Light FindSun()
        {
            // シーンに保存された太陽（エディタメニューが設定）を優先
            var sun = RenderSettings.sun;
            if (sun != null && sun.type == LightType.Directional) return sun;
#if UNITY_2023_1_OR_NEWER
            var any = FindAnyObjectByType<Light>();
#else
            var any = FindObjectOfType<Light>();
#endif
            return any != null && any.type == LightType.Directional ? any : null;
        }
    }
}
