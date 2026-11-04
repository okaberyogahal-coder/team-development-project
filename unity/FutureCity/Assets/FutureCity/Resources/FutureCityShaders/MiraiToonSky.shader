// ミライフ：アニメ調の空（グラデーション＋太陽＋手描き風の積雲）。RenderSettings.skybox に使います。URP 専用。
// 雲のテクスチャは RG=雲の表面の向き、B=厚み、A=雲の量（リニアのデータ）。太陽の向きに合わせてその場で陰影をつけます。
Shader "MiraiToon/Sky"
{
    Properties
    {
        _CloudTex ("Clouds (RG normal, B thickness, A coverage)", 2D) = "black" {}
        _CloudRotation ("Cloud Rotation (0-1)", Float) = 0
        _CloudLit ("Cloud Lit", Color) = (1,0.99,0.97,1)
        _CloudShade ("Cloud Shade", Color) = (0.66,0.70,0.86,1)
        _CloudRim ("Cloud Rim", Color) = (1,0.96,0.88,1)
        _CloudOpacity ("Cloud Opacity", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SkyVert
            #pragma fragment SkyFrag

            #include "MiraiToonURP.hlsl"

            TEXTURE2D(_CloudTex);
            SAMPLER(sampler_CloudTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudTex_ST;
                float  _CloudRotation;
                half4  _CloudLit;
                half4  _CloudShade;
                half4  _CloudRim;
                half   _CloudOpacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir        : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings SkyVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            half4 SkyFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 dir = normalize(input.dir);
                MTAtmos atm = MT_GetAtmos();
                float3 col = MT_SkyGradient(dir, atm);
                col += MT_SunDisk(dir, atm, _MT_SunColor.rgb);
                if (dir.y > -0.02)
                {
                    float lon = atan2(dir.x, dir.z) / 6.2831853 + 0.5 + _CloudRotation;
                    float lat = asin(saturate(dir.y)) / 1.5707963;
                    float4 c = SAMPLE_TEXTURE2D_LOD(_CloudTex, sampler_CloudTex, float2(lon, lat), 0);
                    float3 T = normalize(cross(float3(0.0, 1.0, 0.0), dir) + float3(0.0001, 0.0, 0.0));
                    float3 B = cross(dir, T);
                    float2 nxy = c.rg * 2.0 - 1.0;
                    float nz = sqrt(max(0.0, 1.0 - dot(nxy, nxy)));
                    float3 n = normalize(T * nxy.x + B * nxy.y - dir * nz);
                    MTCloudShade cs = MT_CloudShade(n, dir, c.b, atm, _CloudLit.rgb, _CloudShade.rgb, _CloudRim.rgb, _MT_SunColor.rgb);
                    float a = c.a * smoothstep(-0.02, 0.03, dir.y) * _CloudOpacity;
                    col = lerp(col, cs.color, a);
                }
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
