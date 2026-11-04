// ミライフ：アニメ調のガラス（うすい水色・空の映り込み・ななめの光の帯・太陽のハイライト）。URP 専用。
Shader "MiraiToon/Glass"
{
    Properties
    {
        _BaseColor ("Tint (A = base opacity)", Color) = (0.70,0.88,0.98,0.22)
        _RimColor ("Rim Color", Color) = (0.85,0.95,1,1)
        _StreakColor ("Streak Color", Color) = (1,1,1,1)
        _ReflStrength ("Reflection", Range(0,1)) = 0.75
        _StreakStrength ("Streak Strength", Range(0,2)) = 0.55
        _StreakScale ("Streak Scale", Float) = 0.22
        _SpecIntensity ("Sun Highlight", Range(0,6)) = 2.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "GlassForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GlassVert
            #pragma fragment GlassFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_instancing

            #include "MiraiToonURP.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half4 _StreakColor;
                half  _ReflStrength;
                half  _StreakStrength;
                half  _StreakScale;
                half  _SpecIntensity;
                half  _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings GlassVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 GlassFrag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 n = normalize(input.normalWS);
                n = IS_FRONT_VFACE(face, true, false) ? n : -n;
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                MTAtmos atm = MT_GetAtmos();
                MTLight L = MT_GetMainLight(input.positionWS);
                float3 R = reflect(-v, n);
                half3 env = GlossyEnvironmentReflection(R, (half)0.05, (half)1.0);
                MTGlassResult g = MT_ShadeGlass(input.positionWS, n, v, L, atm, (float3)env, _BaseColor.rgb, _BaseColor.a,
                    _RimColor.rgb, _StreakColor.rgb, _ReflStrength, _StreakStrength, _StreakScale, _SpecIntensity);
                float3 col = MT_ApplyFog(g.color, _WorldSpaceCameraPos, input.positionWS, atm);
                return half4(col, g.alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
