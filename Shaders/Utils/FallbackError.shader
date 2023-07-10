Shader "Hidden/Universal Render Pipeline/FallbackError"
{
    SubShader
    {
        Tags {"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "ShaderModel" = "4.5"}

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma editor_sync_compilation

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 normal : NORMAL;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 glow : TEXCOORD0;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = TransformObjectToHClip(v.vertex.xyz);

                float3 wPos = mul(unity_ObjectToWorld, v.vertex);
                float3 normal = normalize(TransformObjectToWorldNormal(v.normal));

                const float3 errorColor = float3(255, 0, 0) / (255.0).xxx;

				float3 view = normalize(_WorldSpaceCameraPos - wPos);
				float fac = pow(saturate(dot(normalize(normal), view)), 3).r * abs((sin(_Time.x * 200) + 1) / 2);

                o.glow = float4(errorColor * fac, 1);

                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return i.glow;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Core/FallbackError"
}
