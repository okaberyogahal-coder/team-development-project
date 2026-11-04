// ミライフ：アニメ調の水（深さで色が変わる・空が映る・きらめき・岸の泡）。URP 専用。
// 岸の泡と深さの色には、URP の「Depth Texture」（カメラの深度）を使います（MiraiAtmosphere が自動でオンにします）。
Shader "MiraiToon/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.42,0.86,0.84,1)
        _DeepColor ("Deep Color", Color) = (0.10,0.42,0.70,1)
        _FoamColor ("Foam Color", Color) = (1,1,1,1)
        _DepthRange ("Depth Range (m)", Float) = 1.6
        _FoamWidth ("Foam Width (m)", Float) = 0.3
        _WaveStrength ("Wave Strength", Range(0,2)) = 0.35
        _WaveScale ("Wave Scale", Range(0,4)) = 1
        _ReflStrength ("Reflection", Range(0,1)) = 0.85
        _Sparkle ("Sparkle", Range(0,8)) = 2.5
        _Alpha ("Shallow Alpha", Range(0,1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVert
            #pragma fragment WaterFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing

            #include "MiraiToonURP.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float _DepthRange;
                float _FoamWidth;
                half  _WaveStrength;
                half  _WaveScale;
                half  _ReflStrength;
                half  _Sparkle;
                half  _Alpha;
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
                float  eyeDepth   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings WaterVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.eyeDepth = -TransformWorldToView(p).z;
                return o;
            }

            half4 WaterFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 ssuv = GetNormalizedScreenSpaceUV(input.positionCS);
                float raw = SampleSceneDepth(ssuv);
                float sceneEye = LinearEyeDepth(raw, _ZBufferParams);
                float waterDepth = max(sceneEye - input.eyeDepth, 0.0);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                MTAtmos atm = MT_GetAtmos();
                MTLight L = MT_GetMainLight(input.positionWS);
                MTWaterResult w = MT_ShadeWater(input.positionWS, v, L, atm, waterDepth, _Time.y,
                    _ShallowColor.rgb, _DeepColor.rgb, _FoamColor.rgb, _DepthRange, _FoamWidth,
                    _WaveStrength * _WaveScale, _ReflStrength, _Sparkle, _Alpha);
                float3 col = MT_ApplyFog(w.color, _WorldSpaceCameraPos, input.positionWS, atm);
                return half4(col, w.alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
