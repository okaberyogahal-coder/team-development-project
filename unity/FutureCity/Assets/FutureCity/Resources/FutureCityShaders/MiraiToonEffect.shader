// ミライフ：光の効果（ビーム・火花・シールドの輪）。加算合成・ライティングなし・頂点色で色と強さ（a）を渡す。URP 専用。
// UV の v（0〜1）の中央ほど明るい（帯の縁がやわらかく消える）。
Shader "MiraiToon/Effect"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _EdgeSoft ("Edge Softness", Range(0.01, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Name "ForwardEffect"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex EffectVert
            #pragma fragment EffectFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                half _EdgeSoft;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings EffectVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color;
                o.uv = input.uv;
                return o;
            }

            half4 EffectFrag(Varyings input) : SV_Target
            {
                half across = 1.0 - abs(input.uv.y * 2.0 - 1.0);         // 帯の中央=1、縁=0
                half edge = smoothstep(0.0, _EdgeSoft, across);
                half3 c = _BaseColor.rgb * input.color.rgb * (_Intensity * input.color.a * edge);
                return half4(c, 1.0);
            }
            ENDHLSL
        }
    }
}
