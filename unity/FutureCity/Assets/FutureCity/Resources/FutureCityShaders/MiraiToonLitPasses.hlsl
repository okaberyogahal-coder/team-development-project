// =====================================================================================
//  MiraiToonLitPasses.hlsl — MiraiToon/Lit・LitOutline・Foliage・Grass の各パス
//
//  シェーダー側で #define して使い分けます（キーワードを増やさないため）。
//    MT_FOLIAGE  … 葉：アルファで切り抜き・両面・透過光・風
//    MT_GRASS    … 草：根元→先端の色・風・プレイヤーが通ると押し分ける
//    MT_ALPHACLIP… アルファで切り抜き
//    MT_DOUBLE_SIDED … 裏面は法線を反転して照らす（葉・草は反転しない）
// =====================================================================================
#ifndef MIRAI_TOON_LIT_PASSES_INCLUDED
#define MIRAI_TOON_LIT_PASSES_INCLUDED

#include "MiraiToonURP.hlsl"
#include "MiraiToonLitInput.hlsl"

struct MTAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    float4 uv2        : TEXCOORD1;   // x=風の効き y=予備
    half4  color      : COLOR;       // rgb=色むら a=AO
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct MTVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
    half4  color      : TEXCOORD3;
    float4 extra      : TEXCOORD4;   // x=草の高さ(0〜1) y=風の効き
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// ---- 頂点の位置（風・草の押し分け）を世界座標で決める。全パス共通
float3 MT_DeformWS(float3 positionWS, MTAttributes input, out float bladeT)
{
    bladeT = 0.0;
#if defined(MT_GRASS)
    bladeT = saturate(input.uv.y);
    float w = bladeT * bladeT;
    positionWS += MT_WindOffset(positionWS, w * _WindWeight, _Time.y, _MT_Wind);
    // プレイヤーの近くは外側へ押し分ける
    float2 d = positionWS.xz - _MT_Player.xz;
    float dist = length(d);
    float r = max(_MT_Player.w, 0.01);
    float push = saturate(1.0 - dist / r) * w;
    float vertical = saturate(1.0 - abs(positionWS.y - _MT_Player.y) * 0.5);
    push *= vertical;
    positionWS.xz += (d / max(dist, 0.001)) * (push * 0.55);
    positionWS.y -= push * 0.25;
#else
    if (_WindWeight > 0.0)
        positionWS += MT_WindOffset(positionWS, input.uv2.x * _WindWeight, _Time.y, _MT_Wind);
#endif
    return positionWS;
}

// =====================================================================================
//  ForwardLit
// =====================================================================================
MTVaryings MTLitVert(MTAttributes input)
{
    MTVaryings output = (MTVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float bladeT;
    positionWS = MT_DeformWS(positionWS, input, bladeT);
    output.positionWS = positionWS;
    output.positionCS = TransformWorldToHClip(positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.color = input.color;
    output.extra = float4(bladeT, input.uv2.x, input.uv2.y, 0.0);
    return output;
}

half4 MTLitFrag(MTVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float3 positionWS = input.positionWS;
    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    float3 albedo = tex.rgb * _BaseColor.rgb;
    float alpha = tex.a * _BaseColor.a;

#if defined(MT_ALPHACLIP)
    // MSAA のときはアルファ・トゥ・カバレッジでなめらかに、MSAA なしでも同じ位置で切れる
    float aw = max(fwidth(alpha), 0.0001);
    alpha = saturate((alpha - _Cutoff) / aw + 0.5);
    clip(alpha - 0.01);
#endif

    float3 vcol = lerp(float3(1.0, 1.0, 1.0), (float3)input.color.rgb, _VertexColorStrength);
    float vao = lerp(1.0, input.color.a, _AOStrength);
    albedo *= vcol;

#if defined(MT_GRASS)
    float t = input.extra.x;
    albedo = lerp(_RootColor.rgb, _TipColor.rgb, smoothstep(0.0, 1.0, t)) * vcol * tex.rgb;
    vao = lerp(0.55, 1.0, smoothstep(0.0, 0.7, t)) * lerp(1.0, input.color.a, _AOStrength);
#endif

    // 絵の具のゆらぎ（同じ色の面がのっぺりしないように、世界座標の低周波ノイズで明るさを少し揺らす）
    float nz = MT_Fbm3(positionWS * _NoiseScale);
    albedo *= 1.0 + (nz - 0.5) * _NoiseStrength;

    float3 n = normalize(input.normalWS);
#if defined(MT_DOUBLE_SIDED) && !defined(MT_FOLIAGE) && !defined(MT_GRASS)
    // 葉と草は「かたまり（樹冠・株）の外向き」の法線を使うので、裏面でも反転しない（反転するとカードごとに明暗がばらつく）
    n = IS_FRONT_VFACE(face, true, false) ? n : -n;
#endif
#if defined(MT_GRASS)
    n = normalize(lerp(n, float3(0.0, 1.0, 0.0), 0.75));
#endif

    float3 v = GetWorldSpaceNormalizeViewDir(positionWS);
    float2 ssuv = GetNormalizedScreenSpaceUV(input.positionCS);

    float ssaoInd, ssaoDir;
    MT_GetSSAO(ssuv, ssaoInd, ssaoDir);

    MTAtmos atm = MT_GetAtmos();
    MTLight L = MT_GetMainLight(positionWS);
    L.shadow *= ssaoDir;

    MTSurface s;
    s.albedo = albedo;
    s.normal = n;
    s.occlusion = vao * ssaoInd;
    s.shadeTint = _ShadeColor.rgb;
    s.shadeShift = _ShadeShift;
    s.shadeSoft = max(_ShadeSoft, 0.001);
    s.aoDirect = _AODirect;
    s.specIntensity = _SpecIntensity;
    s.specSize = _SpecSize;
    s.specSoft = max(_SpecSoft, 0.0005);
    s.specColor = _SpecColor.rgb;
    s.rimIntensity = _RimIntensity;
    s.rimPower = max(_RimPower, 0.1);
    s.rimColor = _RimColor.rgb;
    s.shadowSharp = _ShadowSharp;
    s.translucency = _Translucency;
    s.emission = _EmissionColor.rgb;

    MTShadeResult r = MT_ShadeMain(s, v, L, atm);
    float3 col = r.color;
    col += MT_AdditionalLights(s, positionWS, ssuv);

    // 映り込み（ガラス・金属）：反射プローブ（空）を、斜めから見るほど強く
    if (_ReflStrength > 0.001)
    {
        float3 R = reflect(-v, n);
        half3 env = GlossyEnvironmentReflection(R, (half)(1.0 - _Smoothness), (half)s.occlusion);
        float fres = 0.08 + 0.92 * pow(1.0 - saturate(dot(n, v)), 4.0);
        col = lerp(col, (float3)env, saturate(fres * _ReflStrength));
    }

    // 空気遠近
    float3 fogged = MT_ApplyFog(col, _WorldSpaceCameraPos, positionWS, atm);
    col = lerp(col, fogged, _FogScale);

    return half4(col, alpha);
}

// =====================================================================================
//  輪郭線（人物・小物）。LightMode = SRPDefaultUnlit。裏面を少しふくらませて描く
// =====================================================================================
MTVaryings MTOutlineVert(MTAttributes input)
{
    MTVaryings output = (MTVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float bladeT;
    positionWS = MT_DeformWS(positionWS, input, bladeT);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.positionWS = positionWS;
    output.normalWS = normalWS;
    output.positionCS = MT_OutlinePositionCS(positionWS, normalWS, _OutlineWidth);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.color = input.color;
    return output;
}

half4 MTOutlineFrag(MTVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    float3 base = tex.rgb * _BaseColor.rgb * lerp(float3(1.0, 1.0, 1.0), (float3)input.color.rgb, _VertexColorStrength);
    // 線の色：材質の色を暗く・濃くした色（真っ黒にしない）
    float3 col = MT_Saturation(base, 1.4) * _OutlineColor.rgb;
    MTAtmos atm = MT_GetAtmos();
    float3 fogged = MT_ApplyFog(col, _WorldSpaceCameraPos, input.positionWS, atm);
    col = lerp(col, fogged, _FogScale);
    return half4(col, 1.0);
}

// =====================================================================================
//  ShadowCaster
// =====================================================================================
float3 _LightDirection;
float3 _LightPosition;

struct MTShadowVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

MTShadowVaryings MTShadowVert(MTAttributes input)
{
    MTShadowVaryings output = (MTShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float bladeT;
    positionWS = MT_DeformWS(positionWS, input, bladeT);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    output.positionCS = ApplyShadowClamping(positionCS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

half4 MTShadowFrag(MTShadowVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
#if defined(MT_ALPHACLIP)
    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
    clip(a - _Cutoff);
#endif
    return 0;
}

// =====================================================================================
//  DepthOnly / DepthNormals（SSAO・被写界深度などが使う）
// =====================================================================================
struct MTDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

MTDepthVaryings MTDepthVert(MTAttributes input)
{
    MTDepthVaryings output = (MTDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float bladeT;
    positionWS = MT_DeformWS(positionWS, input, bladeT);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half4 MTDepthFrag(MTDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(MT_ALPHACLIP)
    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
    clip(a - _Cutoff);
#endif
    return input.positionCS.z;
}

half4 MTDepthNormalsFrag(MTDepthVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(MT_ALPHACLIP)
    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
    clip(a - _Cutoff);
#endif
    float3 normalWS = normalize(input.normalWS);
#if defined(MT_DOUBLE_SIDED) && !defined(MT_FOLIAGE) && !defined(MT_GRASS)
    normalWS = IS_FRONT_VFACE(face, true, false) ? normalWS : -normalWS;
#endif
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
    return half4(packedNormalWS, 0.0);
#else
    return half4(normalWS, 0.0);
#endif
}

#endif // MIRAI_TOON_LIT_PASSES_INCLUDED
