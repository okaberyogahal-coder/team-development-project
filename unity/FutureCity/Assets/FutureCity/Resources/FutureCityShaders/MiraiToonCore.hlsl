// =====================================================================================
//  MiraiToonCore.hlsl — ミライフのアニメ調ライティングの「計算だけ」をまとめたファイル
//
//  ・Unity（URP の HLSL）と、検証用プレビュー（GLSL）の両方で同じ式を使うため、
//    両方の言語で通る書き方だけを使っています（float3(...) は 3 成分で書く、pow は同じ型どうし、など）。
//  ・URP 固有の処理（ライトや影の取得）は MiraiToonURP.hlsl 側にあります。
//  ・色はすべてリニア空間です。
// =====================================================================================
#ifndef MIRAI_TOON_CORE_INCLUDED
#define MIRAI_TOON_CORE_INCLUDED

#define MT_F3(x) float3((x), (x), (x))
#define MT_F2(x) float2((x), (x))

static const float MT_PI = 3.14159265;

// ------------------------------------------------------------------ 乱数・ノイズ（絵の具のゆらぎ用）
float MT_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += MT_F3(dot(p, p.zyx + float3(31.32, 31.32, 31.32)));
    return frac((p.x + p.y) * p.z);
}

float MT_Hash12(float2 p)
{
    float3 p3 = frac(float3(p.x, p.y, p.x) * 0.1031);
    p3 += MT_F3(dot(p3, p3.yzx + float3(33.33, 33.33, 33.33)));
    return frac((p3.x + p3.y) * p3.z);
}

float MT_ValueNoise3(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (float3(3.0, 3.0, 3.0) - 2.0 * f);
    float n000 = MT_Hash13(i);
    float n100 = MT_Hash13(i + float3(1.0, 0.0, 0.0));
    float n010 = MT_Hash13(i + float3(0.0, 1.0, 0.0));
    float n110 = MT_Hash13(i + float3(1.0, 1.0, 0.0));
    float n001 = MT_Hash13(i + float3(0.0, 0.0, 1.0));
    float n101 = MT_Hash13(i + float3(1.0, 0.0, 1.0));
    float n011 = MT_Hash13(i + float3(0.0, 1.0, 1.0));
    float n111 = MT_Hash13(i + float3(1.0, 1.0, 1.0));
    float a = lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y);
    float b = lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y);
    return lerp(a, b, f.z);
}

float MT_ValueNoise2(float2 x)
{
    float2 i = floor(x);
    float2 f = frac(x);
    f = f * f * (float2(3.0, 3.0) - 2.0 * f);
    float a = MT_Hash12(i);
    float b = MT_Hash12(i + float2(1.0, 0.0));
    float c = MT_Hash12(i + float2(0.0, 1.0));
    float d = MT_Hash12(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// 2 オクターブ（遠くで細かすぎないように）
float MT_Fbm3(float3 x)
{
    return MT_ValueNoise3(x) * 0.65 + MT_ValueNoise3(x * 2.13 + float3(5.1, 1.7, 9.2)) * 0.35;
}

float MT_Luma(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

// 彩度を上げ下げ（影色を少し鮮やかにするのに使う）
float3 MT_Saturation(float3 c, float s)
{
    float l = MT_Luma(c);
    return max(MT_F3(0.0), MT_F3(l) + (c - MT_F3(l)) * s);
}

// ------------------------------------------------------------------ 入力のまとめ
// dir は「表面から光へ向かう」向き。shadow は 0=影 1=日なた。
struct MTLight
{
    float3 dir;
    float3 color;
    float shadow;
};

// 空・環境光・空気遠近（霧）。C# の MiraiAtmosphere が毎フレーム全シェーダー共通で設定する。
struct MTAtmos
{
    float3 sunDir;          // 太陽の方向（世界座標、上向きが正）
    float3 ambSky;          // 上から来る環境光
    float3 ambEquator;      // 横から
    float3 ambGround;       // 下（地面の照り返し）
    float3 skyZenith;       // 空の色：真上
    float3 skyMid;          //          中ほど
    float3 skyHorizon;      //          地平線
    float3 sunHaze;         // 太陽の方向の霞の色
    float  fogDensity;      // 霧の濃さ（1m あたり）
    float  fogHeightFalloff;// 高さで薄くなる割合
    float  fogBaseHeight;   // 霧の基準の高さ
    float  fogStart;        // この距離までは霧をかけない
    float  fogMax;          // 霧の最大の濃さ
};

// ------------------------------------------------------------------ 環境光（半球）
float3 MT_AmbientLight(float3 n, MTAtmos a)
{
    float up = n.y;
    float3 c = lerp(a.ambEquator, a.ambSky, smoothstep(0.0, 1.0, up));
    c = lerp(c, a.ambGround, smoothstep(0.0, -1.0, up));
    return c;
}

// ------------------------------------------------------------------ トゥーン：明暗の段（ランプ）
float MT_Ramp(float ndl, float shift, float softness)
{
    return smoothstep(shift - softness, shift + softness, ndl);
}

// 影マップの境目を少しだけ締める（ぼけた影の縁をアニメ調の線に近づける）
float MT_ShadowSharpen(float shadowAtten, float sharpness)
{
    float lo = 0.5 - 0.5 * (1.0 - sharpness);
    float hi = 0.5 + 0.5 * (1.0 - sharpness);
    return smoothstep(lo, hi, shadowAtten);
}

// 表面の設定（MiraiToonLit の材質パラメータ）
struct MTSurface
{
    float3 albedo;
    float3 normal;
    float  occlusion;       // 頂点AO × SSAO
    float3 shadeTint;       // 影側にかける色（青紫寄りにするとアニメらしい）
    float  shadeShift;      // 明暗の境目の位置（-1〜1）
    float  shadeSoft;       // 明暗の境目のやわらかさ
    float  aoDirect;        // AO を直接光にもかける割合
    float  specIntensity;
    float  specSize;
    float  specSoft;
    float3 specColor;
    float  rimIntensity;
    float  rimPower;
    float3 rimColor;
    float  shadowSharp;     // 影の縁の締め具合（0=そのまま 1=くっきり）
    float  translucency;    // 葉の透過光
    float3 emission;
};

struct MTShadeResult
{
    float3 color;
    float  litMask;
};

// メインのライティング（主光源 1 つ分）
MTShadeResult MT_ShadeMain(MTSurface s, float3 v, MTLight L, MTAtmos a)
{
    MTShadeResult r;
    float ndl = dot(s.normal, L.dir);
    float ramp = MT_Ramp(ndl, s.shadeShift, s.shadeSoft);
    float sh = MT_ShadowSharpen(L.shadow, s.shadowSharp);
    float lit = ramp * sh;
    r.litMask = lit;

    float3 amb = MT_AmbientLight(s.normal, a);
    // 影側は青紫に寄せ、少し鮮やかに（灰色にくすませない）
    float3 shadeAlbedo = MT_Saturation(s.albedo, 1.18) * s.shadeTint;
    float3 albedoMix = lerp(shadeAlbedo, s.albedo, lit);
    float3 ambient = amb * s.occlusion;
    float directAO = lerp(1.0, s.occlusion, s.aoDirect);
    float3 col = albedoMix * ambient + s.albedo * L.color * (lit * directAO);

    // 様式化したハイライト（つやのある素材だけ）
    float3 h = normalize(L.dir + v);
    float nh = saturate(dot(s.normal, h));
    float spec = smoothstep(1.0 - s.specSize - s.specSoft, 1.0 - s.specSize + s.specSoft, nh);
    col += s.specColor * L.color * (spec * lit * s.specIntensity);

    // リムライト：日なた側は光の色、影側は空の色で、輪郭をふちどる
    float nv = saturate(dot(s.normal, v));
    float fres = pow(1.0 - nv, s.rimPower);
    float side = saturate(dot(s.normal, L.dir) * 0.5 + 0.5);
    float3 rimLight = lerp(amb * 0.55, L.color * 0.9, side * sh);
    col += s.rimColor * rimLight * (fres * s.rimIntensity);

    // 葉の透過光（逆光で黄緑に光る）
    float back = pow(saturate(dot(v, -L.dir)), 3.0);
    float wrapB = saturate(0.5 - 0.5 * ndl);
    col += s.albedo * L.color * (back * wrapB * s.translucency * sh) * 1.6;

    col += s.emission;
    r.color = col;
    return r;
}

// 追加のライト（点光源など）はやわらかいランプで足す
float3 MT_ShadeAdditional(MTSurface s, float3 lightDir, float3 lightColor, float atten)
{
    float ndl = dot(s.normal, lightDir);
    float ramp = MT_Ramp(ndl, -0.1, 0.35);
    return s.albedo * lightColor * (ramp * atten);
}

// ------------------------------------------------------------------ 空のグラデーション
float3 MT_SkyGradient(float3 dir, MTAtmos a)
{
    float y = dir.y;
    float t1 = smoothstep(-0.02, 0.22, y);
    float t2 = smoothstep(0.12, 0.85, y);
    float3 c = lerp(a.skyHorizon, a.skyMid, t1);
    c = lerp(c, a.skyZenith, t2);
    // 地平線の下は、霞んだ地平線の色を少し暗くした色
    float3 below = a.skyHorizon * float3(0.93, 0.95, 0.98);
    c = lerp(c, below, smoothstep(0.0, -0.25, y));
    // 太陽の方向の地平線をあたたかく明るく
    float sd = saturate(dot(normalize(float3(dir.x, 0.0, dir.z) + float3(0.0001, 0.0, 0.0)),
                            normalize(float3(a.sunDir.x, 0.0, a.sunDir.z) + float3(0.0001, 0.0, 0.0))));
    float horizonBand = 1.0 - smoothstep(0.0, 0.35, abs(y));
    c = lerp(c, a.sunHaze, pow(sd, 6.0) * horizonBand * 0.55);
    return c;
}

// 太陽の円盤と光のにじみ
float3 MT_SunDisk(float3 dir, MTAtmos a, float3 sunColor)
{
    float sd = saturate(dot(dir, a.sunDir));
    float disk = smoothstep(0.99955, 0.99975, sd);
    float glow = pow(sd, 900.0) * 1.5 + pow(sd, 120.0) * 0.32 + pow(sd, 16.0) * 0.10;
    return sunColor * (disk * 14.0 + glow);
}

// ------------------------------------------------------------------ 空気遠近（霧）
// 霧の色：見る方向の地平線の空の色（遠くの建物が空に溶ける）
float3 MT_FogColor(float3 viewDir, MTAtmos a)
{
    float3 d = normalize(float3(viewDir.x, max(viewDir.y, 0.0) * 0.35, viewDir.z));
    float3 c = MT_SkyGradient(d, a);
    float sd = saturate(dot(viewDir, a.sunDir));
    c += a.sunHaze * (pow(sd, 10.0) * 0.25);
    return c;
}

// 霧の量（高さで薄くなる指数関数の大気を、カメラから点まで積分した値）
float MT_FogAmount(float3 camPos, float3 posWS, MTAtmos a)
{
    float3 d = posWS - camPos;
    float dist = length(d);
    float effDist = max(0.0, dist - a.fogStart);
    float k = max(a.fogHeightFalloff, 0.00001);
    float h0 = camPos.y - a.fogBaseHeight;
    float dyk = k * d.y;
    float heightFactor = abs(dyk) > 0.0001 ? (1.0 - exp(-dyk)) / dyk : 1.0;
    float optical = a.fogDensity * exp(-k * h0) * heightFactor * effDist;
    return saturate(1.0 - exp(-optical)) * a.fogMax;
}

float3 MT_ApplyFog(float3 col, float3 camPos, float3 posWS, MTAtmos a)
{
    float3 v = posWS - camPos;
    float f = MT_FogAmount(camPos, posWS, a);
    float3 fc = MT_FogColor(normalize(v), a);
    return lerp(col, fc, f);
}

// ------------------------------------------------------------------ 風（草・葉・つる）
// weight: 0=根元（動かない）〜1=先端。windParams = (向きx, 向きz, 強さ, 速さ)
float3 MT_WindOffset(float3 posWS, float weight, float time, float4 windParams)
{
    float2 wdir = normalize(float2(windParams.x, windParams.y) + float2(0.0001, 0.0));
    float phase = dot(float2(posWS.x, posWS.z), wdir) * 0.18 - time * windParams.w;
    // 大きなうねり（突風が広場を渡っていく）＋細かいゆれ
    float gust = MT_ValueNoise2(float2(posWS.x, posWS.z) * 0.035 + wdir * (time * 0.35)) ;
    float wave = sin(phase * 6.2831) * 0.5 + 0.5;
    float flutter = sin(time * 7.3 + posWS.x * 2.1 + posWS.z * 1.7) * 0.25;
    float amt = windParams.z * weight * (0.35 + gust * 0.9) * (0.6 + wave * 0.6);
    float3 off = float3(wdir.x, 0.0, wdir.y) * amt;
    off += float3(flutter * 0.4, 0.0, flutter * 0.3) * windParams.z * weight * 0.5;
    off.y = -length(off) * 0.25 * weight;   // しなると少し下がる
    return off;
}

// ------------------------------------------------------------------ 水
// 水面の法線（スクロールする 2 方向のノイズ波）
float3 MT_WaterNormal(float2 xz, float time, float strength)
{
    float e = 0.15;
    float2 p1 = xz * 0.55 + float2(time * 0.05, time * 0.032);
    float2 p2 = xz * 1.3 + float2(-time * 0.04, time * 0.06);
    float h0 = MT_ValueNoise2(p1) * 0.6 + MT_ValueNoise2(p2) * 0.4;
    float hx = MT_ValueNoise2(p1 + float2(e * 0.55, 0.0)) * 0.6 + MT_ValueNoise2(p2 + float2(e * 1.3, 0.0)) * 0.4;
    float hz = MT_ValueNoise2(p1 + float2(0.0, e * 0.55)) * 0.6 + MT_ValueNoise2(p2 + float2(0.0, e * 1.3)) * 0.4;
    return normalize(float3((h0 - hx) / e * strength, 1.0, (h0 - hz) / e * strength));
}

struct MTWaterResult
{
    float3 color;
    float  alpha;
};

// アニメ調の水：深さで色が変わる・空が映る・太陽のきらめき・岸の白い泡
MTWaterResult MT_ShadeWater(float3 posWS, float3 v, MTLight L, MTAtmos a, float waterDepth, float time,
                            float3 shallow, float3 deep, float3 foamColor, float depthRange, float foamWidth,
                            float waveStrength, float reflStrength, float sparkle, float baseAlpha)
{
    MTWaterResult r;
    float3 n = MT_WaterNormal(posWS.xz, time, waveStrength);
    float depthT = saturate(waterDepth / max(depthRange, 0.01));
    float3 body = lerp(shallow, deep, smoothstep(0.0, 1.0, depthT));
    float3 up = float3(0.0, 1.0, 0.0);
    float3 amb = MT_AmbientLight(up, a);
    float sh = L.shadow;
    float3 lit = body * (amb * 0.85 + L.color * (saturate(dot(up, L.dir)) * lerp(0.35, 1.0, sh) * 0.55));

    float3 R = reflect(-v, n);
    float3 sky = MT_SkyGradient(normalize(float3(R.x, abs(R.y), R.z)), a);
    float fres = 0.02 + 0.98 * pow(1.0 - saturate(dot(n, v)), 5.0);
    float3 col = lerp(lit, sky, saturate(fres * reflStrength));

    // 太陽のきらめき（細かく途切れる、アニメの水面の白い点々）
    float3 h = normalize(L.dir + v);
    float nh = saturate(dot(n, h));
    float glint = smoothstep(0.9955, 0.9985, nh);
    float sn = MT_ValueNoise2(posWS.xz * 3.1 + float2(time * 0.7, -time * 0.45));
    glint *= smoothstep(0.42, 0.72, sn);
    col += L.color * (glint * sparkle * sh);

    // 岸の泡（浅いところに、ゆらぐ白い帯）
    float foamBand = 1.0 - smoothstep(0.0, max(foamWidth, 0.001), waterDepth);
    float fn = MT_ValueNoise2(posWS.xz * 2.4 + float2(time * 0.18, time * 0.11));
    float foam = smoothstep(0.42, 0.58, foamBand + (fn - 0.5) * 0.55);
    col = lerp(col, foamColor * (amb * 0.8 + L.color * lerp(0.45, 0.9, sh)), foam * 0.9);

    r.color = col;
    r.alpha = saturate(lerp(baseAlpha, 1.0, smoothstep(0.0, 1.0, depthT)) + foam * 0.8 + fres * 0.25);
    return r;
}

// ------------------------------------------------------------------ ガラス
struct MTGlassResult
{
    float3 color;
    float  alpha;
};

// アニメ調のガラス：うすい水色・空の映り込み・ななめの白い光の帯・太陽のハイライト
MTGlassResult MT_ShadeGlass(float3 posWS, float3 n, float3 v, MTLight L, MTAtmos a, float3 env,
                            float3 tint, float baseAlpha, float3 rimColor, float3 streakColor,
                            float reflStrength, float streakStrength, float streakScale, float specIntensity)
{
    MTGlassResult r;
    float nv = saturate(dot(n, v));
    float fres = pow(1.0 - nv, 3.0);
    float3 amb = MT_AmbientLight(n, a);
    float3 col = tint * (amb + L.color * (saturate(dot(n, L.dir)) * 0.35));
    col = lerp(col, env, saturate(reflStrength * (0.3 + 0.7 * fres)));

    // ななめの光の帯（2 本 1 組）。面ごとにある・ないがあるように大きなノイズで間引く
    float s = dot(posWS, float3(0.55, 0.85, 0.35)) * streakScale;
    float f = frac(s);
    float band = smoothstep(0.0, 0.015, f) * (1.0 - smoothstep(0.07, 0.085, f))
               + smoothstep(0.13, 0.14, f) * (1.0 - smoothstep(0.165, 0.175, f));
    float sel = smoothstep(0.4, 0.62, MT_ValueNoise3(posWS * 0.06 + float3(3.1, 7.7, 1.3)));
    band *= sel;
    col += streakColor * (band * streakStrength);

    float3 h = normalize(L.dir + v);
    float spec = smoothstep(0.984, 0.994, saturate(dot(n, h)));
    col += L.color * (spec * specIntensity * L.shadow);
    col += rimColor * (fres * 0.3);

    r.color = col;
    r.alpha = saturate(baseAlpha + fres * 0.45 + band * streakStrength * 0.35 + spec * 0.6);
    return r;
}

// ------------------------------------------------------------------ 雲（空のパノラマに描く積雲）
struct MTCloudShade
{
    float3 color;
};

// n: 雲の表面の法線（世界座標）、viewDir: 見ている方向、thickness: 厚み(0〜1)
MTCloudShade MT_CloudShade(float3 n, float3 viewDir, float thickness, MTAtmos a,
                           float3 litColor, float3 shadeColor, float3 rimColor, float3 sunColor)
{
    MTCloudShade r;
    float ndl = dot(n, a.sunDir);
    float lit = smoothstep(-0.32, 0.38, ndl);
    float3 sunTint = sunColor / max(MT_Luma(sunColor), 0.001);
    float3 litC = litColor * lerp(MT_F3(1.0), sunTint, 0.3);
    float3 col = lerp(shadeColor, litC, lit);
    // 雲の下のほう（厚いところの影側）は少し濃く
    col *= lerp(1.0, 0.86, (1.0 - lit) * thickness);
    // 太陽の側の縁が光る（銀の縁取り）
    float toSun = saturate(dot(viewDir, a.sunDir));
    float silver = pow(toSun, 5.0) * (1.0 - thickness);
    col += rimColor * (silver * 0.55);
    float fres = pow(1.0 - saturate(dot(n, -viewDir)), 3.0);
    col += rimColor * (fres * saturate(ndl + 0.35) * 0.22);
    // 低い雲は霞む
    float haze = 1.0 - smoothstep(0.0, 0.3, viewDir.y);
    col = lerp(col, MT_FogColor(viewDir, a), haze * 0.42);
    r.color = col;
    return r;
}

#endif // MIRAI_TOON_CORE_INCLUDED
