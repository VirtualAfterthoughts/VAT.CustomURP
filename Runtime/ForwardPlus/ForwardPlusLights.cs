using Unity.Collections;
using UnityEngine.PlayerLoop;
using Unity.Jobs;
using UnityEngine.Assertions;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;

// zCubed Additions
namespace UnityEngine.Rendering.Universal.Internal
{
    /// <summary>
    /// Computes and submits lighting data to the GPU.
    /// </summary>
    public class ForwardPlusLights
    {
        static class LightConstantBuffer
        {
            public static int _MainLightBuffer;
            public static int _AdditionalLightsBuffer;
        }

        public struct ForwardPlusLightData 
        {
            public Vector4 lightPosition;
            public Vector4 lightColor;
            public Vector4 lightAttenuations;
            public Vector4 lightSpotDirections;
            public Vector4 lightOcclusionProbeChannels;
            public Vector4 lightShapeParams;
        }

        const string k_SetupLightConstants = "Setup Light Constants";
        private static readonly ProfilingSampler m_ProfilingSampler = new ProfilingSampler(k_SetupLightConstants);
        MixedLightingSetup m_MixedLightingSetup;

        float[] m_AdditionalLightsLayerMasks;  // Unity has no support for binding uint arrays. We will use asuint() in the shader instead.

        private LightCookieManager m_LightCookieManager;

        internal struct InitParams
        {
            public LightCookieManager lightCookieManager;
            public int tileSize;

            static internal InitParams GetDefault()
            {
                InitParams p;
                {
                    var settings = LightCookieManager.Settings.GetDefault();
                    var asset = UniversalRenderPipeline.asset;
                    if (asset)
                    {
                        settings.atlas.format = asset.additionalLightsCookieFormat;
                        settings.atlas.resolution = asset.additionalLightsCookieResolution;
                    }

                    p.lightCookieManager = new LightCookieManager(ref settings);
                    p.tileSize = 32;
                }
                return p;
            }
        }

        public ForwardPlusLights() : this(InitParams.GetDefault()) { }

        internal ForwardPlusLights(InitParams initParams)
        {
            Assert.IsTrue(math.ispow2(initParams.tileSize));

            LightConstantBuffer._MainLightBuffer = Shader.PropertyToID("_MainLightBuffer");
            LightConstantBuffer._AdditionalLightsBuffer = Shader.PropertyToID("_AdditionalLightsBuffer");

            m_LightCookieManager = initParams.lightCookieManager;
        }

        internal void ProcessLights(ref RenderingData renderingData)
        {
            //Debug.Log("Processing lights");
        }

        public void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            //Debug.Log("Setup");

            using (new ProfilingScope(null, m_ProfilingSampler))
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                SetupShaderLightConstants(cmd, ref renderingData);
                CoreUtils.SetKeyword(cmd, "_URP_FORWARD_PLUS", true);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        internal void Cleanup()
        {
            //Debug.Log("Cleanup");
        }

        void InitializeLightConstants(NativeArray<VisibleLight> lights, int lightIndex, out Vector4 lightPos, out Vector4 lightColor, out Vector4 lightAttenuation, out Vector4 lightSpotDir, out Vector4 lightOcclusionProbeChannel, out Vector4 shapeParams, out uint lightLayerMask)
        {
            lightPos = Vector4.zero;
            lightColor = Vector4.zero;
            lightAttenuation = Vector4.zero;
            lightOcclusionProbeChannel = Vector4.zero;
            lightSpotDir = Vector4.zero;
            shapeParams = Vector4.zero;
            lightLayerMask = 0;
        }

        void SetupShaderLightConstants(CommandBuffer cmd, ref RenderingData renderingData)
        {
            m_MixedLightingSetup = MixedLightingSetup.None;

            // Main light has an optimized shader path for main light. This will benefit games that only care about a single light.
            // Universal pipeline also supports only a single shadow light, if available it will be the main light.
            SetupMainLightConstants(cmd, ref renderingData.lightData);
            SetupAdditionalLightConstants(cmd, ref renderingData);
        }

        void SetupMainLightConstants(CommandBuffer cmd, ref LightData lightData)
        {

        }

        void SetupAdditionalLightConstants(CommandBuffer cmd, ref RenderingData renderingData)
        {
            
        }
    }
}
