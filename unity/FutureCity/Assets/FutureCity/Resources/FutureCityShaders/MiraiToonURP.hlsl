// =====================================================================================
//  MiraiToonURP.hlsl — URP とつなぐ部分（ライト・影・SSAO・全体共通の空気の設定）
//  計算そのものは MiraiToonCore.hlsl にあります。
// =====================================================================================
#ifndef MIRAI_TOON_URP_INCLUDED
#define MIRAI_TOON_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "MiraiToonCore.hlsl"

// ---- 全シェーダー共通（C# の MiraiAtmosphere が Shader.SetGlobalVector で設定。リニア色）
float4 _MT_SunDir;        // xyz=太陽へ向かう向き, w=1 なら設定済み
float4 _MT_AmbSky;
float4 _MT_AmbEquator;
float4 _MT_AmbGround;
float4 _MT_SkyZenith;
float4 _MT_SkyMid;
float4 _MT_SkyHorizon;
float4 _MT_SunHaze;
float4 _MT_Fog;           // x=濃さ y=高さで薄くなる割合 z=基準の高さ w=かけ始める距離
float4 _MT_Fog2;          // x=最大の濃さ
float4 _MT_Wind;          // x,y=風の向き(xz) z=強さ w=速さ
float4 _MT_Player;        // xyz=プレイヤーの足もと w=草を押し分ける半径
float4 _MT_SunColor;      // 空に描く太陽の色

MTAtmos MT_GetAtmos()
{
    MTAtmos a;
    if (_MT_SunDir.w > 0.5)
    {
        a.sunDir = normalize(_MT_SunDir.xyz);
        a.ambSky = _MT_AmbSky.rgb;
        a.ambEquator = _MT_AmbEquator.rgb;
        a.ambGround = _MT_AmbGround.rgb;
        a.skyZenith = _MT_SkyZenith.rgb;
        a.skyMid = _MT_SkyMid.rgb;
        a.skyHorizon = _MT_SkyHorizon.rgb;
        a.sunHaze = _MT_SunHaze.rgb;
        a.fogDensity = _MT_Fog.x;
        a.fogHeightFalloff = _MT_Fog.y;
        a.fogBaseHeight = _MT_Fog.z;
        a.fogStart = _MT_Fog.w;
        a.fogMax = _MT_Fog2.x;
    }
    else
    {
        // まだ設定されていない（エディタのシーン表示など）ときの、昼の標準値
        a.sunDir = normalize(float3(0.45, 0.62, 0.35));
        a.ambSky = float3(0.52, 0.62, 0.80);
        a.ambEquator = float3(0.50, 0.55, 0.62);
        a.ambGround = float3(0.36, 0.34, 0.30);
        a.skyZenith = float3(0.10, 0.30, 0.78);
        a.skyMid = float3(0.30, 0.56, 0.92);
        a.skyHorizon = float3(0.72, 0.84, 0.96);
        a.sunHaze = float3(1.0, 0.86, 0.66);
        a.fogDensity = 0.0012;
        a.fogHeightFalloff = 0.012;
        a.fogBaseHeight = 0.0;
        a.fogStart = 30.0;
        a.fogMax = 0.92;
    }
    return a;
}

MTLight MT_GetMainLight(float3 positionWS)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    half4 shadowMask = half4(1.0, 1.0, 1.0, 1.0);
    Light ml = GetMainLight(shadowCoord, positionWS, shadowMask);
    MTLight L;
    L.dir = ml.direction;
    L.color = ml.color * ml.distanceAttenuation;
    L.shadow = ml.shadowAttenuation;
    return L;
}

// 追加ライト（点光源）をすべて足す
float3 MT_AdditionalLights(MTSurface s, float3 positionWS, float2 normalizedScreenSpaceUV)
{
    float3 sum = float3(0.0, 0.0, 0.0);
#if defined(_ADDITIONAL_LIGHTS)
    half4 shadowMask = half4(1.0, 1.0, 1.0, 1.0);
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = normalizedScreenSpaceUV;
    uint pixelLightCount = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
    {
        Light dl = GetAdditionalLight(li, positionWS, shadowMask);
        sum += MT_ShadeAdditional(s, dl.direction, dl.color, dl.distanceAttenuation * dl.shadowAttenuation);
    }
    #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        sum += MT_ShadeAdditional(s, light.direction, light.color, light.distanceAttenuation * light.shadowAttenuation);
    LIGHT_LOOP_END
#endif
    return sum;
}

// SSAO（URP の Screen Space Ambient Occlusion を入れたときだけ効く）
void MT_GetSSAO(float2 normalizedScreenSpaceUV, out float indirectAO, out float directAO)
{
    indirectAO = 1.0;
    directAO = 1.0;
#if defined(_SCREEN_SPACE_OCCLUSION)
    AmbientOcclusionFactor aof = GetScreenSpaceAmbientOcclusion(normalizedScreenSpaceUV);
    indirectAO = aof.indirectAmbientOcclusion;
    directAO = aof.directAmbientOcclusion;
#endif
}

// 輪郭線の太さ（画面上でほぼ一定のピクセル幅になるように、距離と画角で補正）
float4 MT_OutlinePositionCS(float3 positionWS, float3 normalWS, float widthPixels)
{
    float4 posCS = TransformWorldToHClip(positionWS);
    float3 nCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS);
    float2 nScreen = normalize(nCS.xy + float2(0.00001, 0.0));
    // _ScreenParams.xy = 画面のピクセル数。w を掛けて遠近の割り算を打ち消す
    float2 offs = nScreen * (widthPixels * 2.0) / _ScreenParams.xy * posCS.w;
    // 遠くでは細く（最大でも距離 30m 相当で止める）
    float distFade = saturate(30.0 / max(posCS.w, 0.001));
    posCS.xy += offs * lerp(0.35, 1.0, distFade);
    return posCS;
}

#endif // MIRAI_TOON_URP_INCLUDED
