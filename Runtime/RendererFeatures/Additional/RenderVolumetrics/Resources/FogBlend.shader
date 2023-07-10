Shader "Hidden/Volumetrics/FogBlend"
{
    Properties
    {
        _MainTex ("Texture", any) = "" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        
        Blend One One
        //Blend One Zero

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma target 5.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = float4(v.vertex.xyz, 1);
                o.uv = v.uv;

                #if UNITY_UV_STARTS_AT_TOP
                o.vertex.y *= -1;
                #endif

                return o;
            }

            #pragma multi_compile _ _DEPTH_MSAA_2 _DEPTH_MSAA_4 _DEPTH_MSAA_8
            #pragma multi_compile _ _USE_DRAW_PROCEDURAL

#if defined(_DEPTH_MSAA_2)
    #define MSAA_SAMPLES 2
#elif defined(_DEPTH_MSAA_4)
    #define MSAA_SAMPLES 4
#elif defined(_DEPTH_MSAA_8)
    #define MSAA_SAMPLES 8
#else
    #define MSAA_SAMPLES 1
#endif

#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    #define DEPTH_TEXTURE_MS(name, samples) Texture2DMSArray<float, samples> name
    #define DEPTH_TEXTURE(name) TEXTURE2D_ARRAY_FLOAT(name)
    #define LOAD(uv, sampleIndex) LOAD_TEXTURE2D_ARRAY_MSAA(_SceneDepth, uv, unity_StereoEyeIndex, sampleIndex)
#else
    #define DEPTH_TEXTURE_MS(name, samples) Texture2DMS<float, samples> name
    #define DEPTH_TEXTURE(name) TEXTURE2D_FLOAT(name)
    #define LOAD(uv, sampleIndex) LOAD_TEXTURE2D_MSAA(_SceneDepth, uv, sampleIndex)
#endif

#if MSAA_SAMPLES == 1
            TEXTURE2D_X(_SceneDepth);
            SAMPLER(sampler_SceneDepth);
#else
            DEPTH_TEXTURE_MS(_SceneDepth, MSAA_SAMPLES);
            float4 _SceneDepth_TexelSize;
#endif

#if UNITY_REVERSED_Z
    #define DEPTH_DEFAULT_VALUE 1.0
    #define DEPTH_OP min
#else
    #define DEPTH_DEFAULT_VALUE 0.0
    #define DEPTH_OP max
#endif

            TEXTURE3D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_NoisePattern);
            SAMPLER(sampler_NoisePattern);

            SAMPLER(sampler_LinearClamp);
            SAMPLER(sampler_PointRepeat);

            float4 _PassData;
            float4 _NoiseData;

            float SampleDepth(float2 uv)
            {
            #if MSAA_SAMPLES == 1
                return SAMPLE_TEXTURE2D_X(_SceneDepth, sampler_SceneDepth, uv).r;
            #else
                int2 coord = int2(uv * _SceneDepth_TexelSize.zw);
                float outDepth = DEPTH_DEFAULT_VALUE;

                UNITY_UNROLL
                for (int i = 0; i < MSAA_SAMPLES; ++i)
                    outDepth = DEPTH_OP(LOAD(coord, i), outDepth);
                return outDepth;
            #endif
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                [branch]
                if (unity_StereoEyeIndex != _PassData.w)
                    return 0;

                // zCubed: 
                //  We need to tile the noise pattern to the screen, so we tile it by the screen resolution
                //  We also need to jitter the sample by the noise pattern
                //const float GOLDEN_RATIO_CONJ = 0.61803398875;

                //float2 uv = (i.uv / _PassData.xy);
                //float4 noise = _NoisePattern.Sample(sampler_PointRepeat, uv);

                //float jitter = frac(noise.r + _NoiseData.w * GOLDEN_RATIO_CONJ) * 0.0;
                float jitter = 0.0;

                // zCubed:
                //  Position inside the volumetric texture is based on depth
                //  We need to get the depth relative to the far and clamp it
                //
                //  In my opinion, sampling this way is better than having a global fog buffer that the URP lit materials sample!
                //  This prevents third party shaders from lacking volumetric support and allows sky interference better
                float d = SampleDepth(i.uv);
                float dist = LinearEyeDepth(d, _ZBufferParams);

                float p = saturate((dist + jitter) / _PassData.z);

                return _MainTex.SampleLevel(sampler_LinearClamp, float3(i.uv, pow(p, 1 / 2.0)), 0);
            }
            ENDHLSL
        }
    }
}
