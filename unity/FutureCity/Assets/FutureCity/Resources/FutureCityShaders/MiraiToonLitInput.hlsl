// =====================================================================================
//  MiraiToonLitInput.hlsl — MiraiToon/Lit・Foliage・Grass 共通の材質パラメータ
//  （SRP Batcher のため、すべてのパスで同じ CBUFFER を使います）
// =====================================================================================
#ifndef MIRAI_TOON_LIT_INPUT_INCLUDED
#define MIRAI_TOON_LIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half4  _ShadeColor;          // 影側の色味（rgb）
    half   _ShadeShift;
    half   _ShadeSoft;
    half   _ShadowSharp;
    half   _AOStrength;          // 頂点カラーの a に入れた AO の効き
    half   _AODirect;
    half   _VertexColorStrength; // 頂点カラーの rgb の効き
    half4  _SpecColor;           // rgb=ハイライトの色
    half   _SpecIntensity;
    half   _SpecSize;
    half   _SpecSoft;
    half4  _RimColor;
    half   _RimIntensity;
    half   _RimPower;
    half4  _EmissionColor;       // HDR
    half   _ReflStrength;        // 映り込みの強さ（ガラス・金属・水）
    half   _Smoothness;
    half   _NoiseStrength;       // 絵の具のゆらぎ
    half   _NoiseScale;
    half   _WindWeight;          // 0=動かない
    half   _FogScale;            // 1=空気遠近をかける
    half   _Cutoff;
    half   _Translucency;
    half   _OutlineWidth;        // ピクセル
    half4  _OutlineColor;
    half4  _TipColor;            // 草：先端の色
    half4  _RootColor;           // 草：根元の色
CBUFFER_END

#endif
