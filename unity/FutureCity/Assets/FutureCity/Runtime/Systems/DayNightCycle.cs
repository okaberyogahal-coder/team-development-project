using UnityEngine;
using UnityEngine.Rendering;

namespace FutureCity
{
    /// <summary>
    /// 昼夜サイクル：太陽・月・空・環境光・霧・窓明かりを時刻に合わせて変化させる。
    /// [ / ] で時刻送り、T でタイムラプス。
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        [Tooltip("昼の時刻（夜にはならない）。7〜16時の範囲に制限されます")]
        [Range(7f, 16f)] public float timeOfDay = 10.5f;
        [Tooltip("シニア向けに環境光を明るめにする")]
        public bool brightMode = true;
        public bool autoAdvance = false;
        public float hoursPerSecond = 0.08f;
        public Light sun;
        public Light moon;
        public MaterialLibrary lib;
        public ReflectionProbe probe;

        float lastEnvTime = -100f, envTimer, probeTimer;

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static readonly float[] Hours = { 0f, 4.5f, 6f, 7.5f, 12f, 16.5f, 18f, 19.2f, 20.5f, 24f };
        static readonly Color[] SunCol =
        {
            C(1f, .6f, .4f), C(1f, .5f, .3f), C(1f, .5f, .3f), C(1f, .82f, .66f), C(1f, .97f, .92f),
            C(1f, .88f, .72f), C(1f, .5f, .25f), C(1f, .4f, .3f), C(1f, .5f, .3f), C(1f, .6f, .4f)
        };
        static readonly Color[] SkyAmb =
        {
            C(.04f, .05f, .11f), C(.05f, .06f, .12f), C(.35f, .28f, .38f), C(.5f, .58f, .72f), C(.55f, .66f, .85f),
            C(.55f, .62f, .78f), C(.42f, .3f, .38f), C(.18f, .13f, .27f), C(.05f, .06f, .13f), C(.04f, .05f, .11f)
        };
        static readonly Color[] FogCol =
        {
            C(.03f, .04f, .09f), C(.04f, .05f, .1f), C(.6f, .42f, .4f), C(.62f, .68f, .76f), C(.66f, .74f, .84f),
            C(.68f, .7f, .74f), C(.62f, .38f, .32f), C(.24f, .16f, .28f), C(.05f, .06f, .12f), C(.03f, .04f, .09f)
        };

        static Color Sample(Color[] cols, float h)
        {
            for (int i = 0; i < Hours.Length - 1; i++)
                if (h >= Hours[i] && h <= Hours[i + 1])
                    return Color.Lerp(cols[i], cols[i + 1], Mathf.InverseLerp(Hours[i], Hours[i + 1], h));
            return cols[cols.Length - 1];
        }

        void Update()
        {
            // 夜は使わない：常に昼（7〜16時）の明るさ
            // 通常はミライフ時計（講座の時刻・予約締切と同じ時計）に太陽を合わせる
            if (autoAdvance) timeOfDay += hoursPerSecond * Time.deltaTime;
            else if (MiraiClock.Started) timeOfDay = MiraiClock.HourOfDay;
            if (timeOfDay > 16f) timeOfDay = 7f;
            timeOfDay = Mathf.Clamp(timeOfDay, 7f, 16f);
            Apply();
        }

        public void Apply()
        {
            float t = timeOfDay / 24f;
            float elev = Mathf.Sin((t - 0.25f) * 2f * Mathf.PI);          // 太陽高度 -1..1
            float day = Mathf.Clamp01(elev * 3f + 0.15f);
            float night = 1f - Mathf.Clamp01(elev * 4f + 0.35f);
            float dusk = Mathf.Exp(-(elev * elev) / 0.02f);

            // 太陽（空の描画のため常に RenderSettings.sun として残す）
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(t * 360f - 90f, -35f, 0f);
                sun.color = Sample(SunCol, timeOfDay);
                bool up = elev > -0.05f;
                sun.intensity = up ? Mathf.Lerp(0.15f, 1.6f, Mathf.Clamp01(elev * 2.5f)) * Mathf.Clamp01((elev + 0.05f) * 10f) : 0.0001f;
                sun.shadows = up ? LightShadows.Soft : LightShadows.None;
                RenderSettings.sun = sun;
            }
            if (moon != null)
            {
                moon.intensity = 0.25f * Mathf.Clamp01((-elev - 0.02f) * 6f);
                moon.enabled = moon.intensity > 0.001f;
            }

            // 空
            var sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_Exposure"))
            {
                sky.SetFloat("_Exposure", Mathf.Lerp(0.55f, 1.2f, day) + dusk * 0.25f);
                sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.7f, 1f, day) + dusk * 0.6f);
                sky.SetColor("_SkyTint", Color.Lerp(C(0.22f, 0.28f, 0.6f), C(0.5f, 0.5f, 0.55f), day));
                sky.SetColor("_GroundColor", Color.Lerp(C(0.03f, 0.035f, 0.06f), C(0.35f, 0.36f, 0.4f), day));
                sky.SetFloat("_SunSize", Mathf.Lerp(0.03f, 0.045f, day));
            }

            // 環境光と霧
            Color skyA = Sample(SkyAmb, timeOfDay);
            Color fogC = Sample(FogCol, timeOfDay);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            float amb = brightMode ? 1.08f : 1f;
            RenderSettings.ambientSkyColor = skyA * amb;
            RenderSettings.ambientEquatorColor = Color.Lerp(skyA, fogC, 0.5f) * 0.85f * amb;
            RenderSettings.ambientGroundColor = brightMode ? new Color(0.36f, 0.33f, 0.28f) : skyA * 0.3f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;   // シーンに保存した霧と同じ種類（ビルドで霧が消えないように）
            RenderSettings.fogColor = fogC;
            RenderSettings.fogDensity = brightMode ? 0.00032f : Mathf.Lerp(0.00065f, 0.00045f, day);

            // 発光（夜ほど明るく）
            if (lib != null)
            {
                lib.Night = night;
                float glow = Mathf.Lerp(0.25f, 1.5f, night);
                lib.SetEmissionScale(Mat.FacadeCool, glow);
                lib.SetEmissionScale(Mat.FacadeWarm, glow);
                lib.SetEmissionScale(Mat.FacadeWhite, glow);
                lib.SetEmissionScale(Mat.FacadeDark, glow);
                lib.SetEmissionScale(Mat.NeonWhite, Mathf.Lerp(0.15f, 1.2f, night));
                lib.SetEmissionScale(Mat.NeonCyan, Mathf.Lerp(0.5f, 1.1f, night));
                lib.SetEmissionScale(Mat.NeonAmber, Mathf.Lerp(0.5f, 1.1f, night));
                lib.SetEmissionScale(Mat.NeonGreen, Mathf.Lerp(0.5f, 1.1f, night));
                lib.SetEmissionScale(Mat.Road, Mathf.Lerp(0.2f, 1f, night));
                lib.SetEmissionScale(Mat.Sidewalk, Mathf.Lerp(0.15f, 1f, night));
                lib.SetEmissionScale(Mat.GlowLeaf, Mathf.Lerp(0.1f, 1f, night));
            }

            // 環境反射の更新（時刻が変わったときだけ）
            envTimer += Time.deltaTime;
            if (envTimer > 0.5f && Mathf.Abs(timeOfDay - lastEnvTime) > 0.02f)
            {
                envTimer = 0f;
                lastEnvTime = timeOfDay;
                DynamicGI.UpdateEnvironment();
            }
            probeTimer += Time.deltaTime;
            if (probe != null && probeTimer > (autoAdvance ? 1.5f : 4f))
            {
                probeTimer = 0f;
                probe.RenderProbe();
            }
        }
    }
}
