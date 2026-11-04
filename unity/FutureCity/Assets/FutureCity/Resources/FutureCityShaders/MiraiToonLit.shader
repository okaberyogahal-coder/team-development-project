// ミライフ：アニメ調の基本シェーダー（建物・地面・小物）
// やわらかい2段の陰影＋青紫の影色＋リムライト＋様式化したハイライト＋空気遠近。URP 専用。
Shader "MiraiToon/Lit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ShadeColor ("Shade Tint", Color) = (0.72,0.74,0.92,1)
        _ShadeShift ("Shade Shift", Range(-1,1)) = 0.02
        _ShadeSoft ("Shade Softness", Range(0.001,1)) = 0.16
        _ShadowSharp ("Shadow Edge Sharpness", Range(0,1)) = 0.45
        _AOStrength ("Vertex AO Strength", Range(0,1)) = 1
        _AODirect ("AO On Direct Light", Range(0,1)) = 0.35
        _VertexColorStrength ("Vertex Color Strength", Range(0,1)) = 1
        _SpecColor ("Spec Color", Color) = (1,1,1,1)
        _SpecIntensity ("Spec Intensity", Range(0,4)) = 0
        _SpecSize ("Spec Size", Range(0,0.5)) = 0.04
        _SpecSoft ("Spec Softness", Range(0.0005,0.2)) = 0.02
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimIntensity ("Rim Intensity", Range(0,2)) = 0.18
        _RimPower ("Rim Power", Range(0.1,16)) = 4
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _ReflStrength ("Reflection", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        _NoiseStrength ("Paint Noise", Range(0,0.5)) = 0.06
        _NoiseScale ("Paint Noise Scale", Float) = 0.35
        _WindWeight ("Wind", Range(0,2)) = 0
        _FogScale ("Aerial Perspective", Range(0,1)) = 1
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _Translucency ("Translucency", Range(0,2)) = 0
        _OutlineWidth ("Outline Width (px)", Range(0,8)) = 0
        _OutlineColor ("Outline Color", Color) = (0.35,0.3,0.38,1)
        _TipColor ("Grass Tip", Color) = (0.75,0.9,0.4,1)
        _RootColor ("Grass Root", Color) = (0.2,0.42,0.18,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "UniversalMaterialType"="Unlit" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MTLitVert
            #pragma fragment MTLitFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include "MiraiToonLitPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MTShadowVert
            #pragma fragment MTShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "MiraiToonLitPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MTDepthVert
            #pragma fragment MTDepthFrag
            #pragma multi_compile_instancing
            #include "MiraiToonLitPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MTDepthVert
            #pragma fragment MTDepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include "MiraiToonLitPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
