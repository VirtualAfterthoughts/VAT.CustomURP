using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal.Additions
{
    public class RenderVolumetricsPass : ScriptableRenderPass
    {
        // Precomputed shader properties so we waste less cycles and expend more memory!
        public static class Properties
        {
            public static readonly int _MainLightShadowParams = Shader.PropertyToID("_MainLightShadowParams");
            public static readonly int _MainLightPosition = Shader.PropertyToID("_MainLightPosition");
            public static readonly int _MainLightColor = Shader.PropertyToID("_MainLightColor");
            public static readonly int _MainShadowmap = Shader.PropertyToID("_MainShadowmap");
            public static readonly int _MainLightWorldToShadow = Shader.PropertyToID("_MainLightWorldToShadow");
            public static readonly int _CascadeShadowSplitSpheres0 = Shader.PropertyToID("_CascadeShadowSplitSpheres0");
            public static readonly int _CascadeShadowSplitSpheres1 = Shader.PropertyToID("_CascadeShadowSplitSpheres1");
            public static readonly int _CascadeShadowSplitSpheres2 = Shader.PropertyToID("_CascadeShadowSplitSpheres2");
            public static readonly int _CascadeShadowSplitSpheres3 = Shader.PropertyToID("_CascadeShadowSplitSpheres3");
            public static readonly int _CascadeShadowSplitSphereRadii = Shader.PropertyToID("_CascadeShadowSplitSphereRadii");

            public static readonly int _AdditionalLightsCount = Shader.PropertyToID("_AdditionalLightsCount");
            public static readonly int _AdditionalLightsPosition = Shader.PropertyToID("_AdditionalLightsPosition");
            public static readonly int _AdditionalLightsColor = Shader.PropertyToID("_AdditionalLightsColor");
            public static readonly int _AdditionalLightsAttenuation = Shader.PropertyToID("_AdditionalLightsAttenuation");
            public static readonly int _AdditionalLightsSpotDir = Shader.PropertyToID("_AdditionalLightsSpotDir");
            public static readonly int _AdditionalLightsFogIndices = Shader.PropertyToID("_AdditionalLightsFogIndices");
            public static readonly int _AdditionalShadowmap = Shader.PropertyToID("_AdditionalShadowmap");
            public static readonly int _AdditionalShadowParams = Shader.PropertyToID("_AdditionalShadowParams");
            public static readonly int _AdditionalLightsWorldToShadow = Shader.PropertyToID("_AdditionalLightsWorldToShadow");

            public static readonly int _CameraToWorld = Shader.PropertyToID("_CameraToWorld");
            public static readonly int _WorldToCamera = Shader.PropertyToID("_WorldToCamera");
            public static readonly int _Projection = Shader.PropertyToID("_Projection");
            public static readonly int _InverseProjection = Shader.PropertyToID("_InverseProjection");
            public static readonly int _InverseViewProjection = Shader.PropertyToID("_InverseViewProjection");

            public static readonly int _SceneDepth = Shader.PropertyToID("_SceneDepth");
            public static readonly int _Result = Shader.PropertyToID("_Result");
            public static readonly int _ResultOther = Shader.PropertyToID("_ResultOther");

            public static readonly int _FogParams = Shader.PropertyToID("_FogParams");

            public static readonly int _DepthMSAA = Shader.PropertyToID("_DepthMSAA");

            public static readonly int _PassData = Shader.PropertyToID("_PassData");

            public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
            public static readonly int _MainTexOther = Shader.PropertyToID("_MainTexOther");
            public static readonly int _EyeIndex = Shader.PropertyToID("_EyeIndex");

            public static readonly int _BakeMatrixInverse = Shader.PropertyToID("_BakeMatrixInverse");
            public static readonly int _BakeOrigin = Shader.PropertyToID("_BakeOrigin");
            public static readonly int _BakeExtents = Shader.PropertyToID("_BakeExtents");
            public static readonly int _ResultSize = Shader.PropertyToID("_ResultSize");

            public static readonly int _NoisePattern = Shader.PropertyToID("_NoisePattern");
            public static readonly int _NoiseData = Shader.PropertyToID("_NoiseData");
        }

        RenderTargetIdentifier fogIdent, fogCompositeIdent;
        int fogWidth, fogHeight, fogDepth;

        int depthMSAA = 1;

        const int FOG_TEX_ID = 2000, FOG_COMPOSITE_TEX_ID = 2001;

        //
        // Properties
        //
        public RenderVolumetrics.RealtimePassInfo realtimeInfo;
        public RenderVolumetrics.BakedPassInfo bakedInfo;

        public RenderVolumetrics.Settings settings;
        public ComputeShader realtimeSamplerCS, bakedSamplerCS, compositorCS;
        public Material blendPS;
        public Texture2D noisePattern;
        public RenderVolumetricsProfile profile;

        bool hasMainLight = false;

        public static Vector4[] additionalLightsColor = new Vector4[RenderVolumetrics.MAX_VISIBLE_LIGHTS];
        public static Vector4[] additionalLightsFogIndices = new Vector4[RenderVolumetrics.MAX_VISIBLE_LIGHTS];

        public override void SetupPreRender(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            foreach (ScriptableRenderPass pass in renderer.allRenderPassQueue)
            {
                //
                // Main light shadowing pass
                //
                if (pass is MainLightShadowCasterPass mainPass)
                {
                    realtimeSamplerCS.SetTexture(realtimeInfo.mainKernel, Properties._MainShadowmap, mainPass.mainLightShadowmapTexture);
                    realtimeSamplerCS.SetMatrixArray(Properties._MainLightWorldToShadow, mainPass.mainLightShadowMatrices);

                    realtimeSamplerCS.SetVector(Properties._CascadeShadowSplitSpheres0, mainPass.cascadeSplitDistances[0]);
                    realtimeSamplerCS.SetVector(Properties._CascadeShadowSplitSpheres1, mainPass.cascadeSplitDistances[1]);
                    realtimeSamplerCS.SetVector(Properties._CascadeShadowSplitSpheres2, mainPass.cascadeSplitDistances[2]);
                    realtimeSamplerCS.SetVector(Properties._CascadeShadowSplitSpheres3, mainPass.cascadeSplitDistances[3]);
                    realtimeSamplerCS.SetVector(Properties._CascadeShadowSplitSphereRadii, new Vector4(
                        mainPass.cascadeSplitDistances[0].w * mainPass.cascadeSplitDistances[0].w,
                        mainPass.cascadeSplitDistances[1].w * mainPass.cascadeSplitDistances[1].w,
                        mainPass.cascadeSplitDistances[2].w * mainPass.cascadeSplitDistances[2].w,
                        mainPass.cascadeSplitDistances[3].w * mainPass.cascadeSplitDistances[3].w)
                    );
                }

                //
                // Additional light shadowing pass
                //
                if (pass is AdditionalLightsShadowCasterPass additionalPass)
                {
                    realtimeSamplerCS.SetTexture(realtimeInfo.additionalKernel, Properties._AdditionalShadowmap, additionalPass.additionalLightsShadowmapTexture);
                    realtimeSamplerCS.SetVectorArray(Properties._AdditionalShadowParams, additionalPass.additionalLightIndexToShadowParams);
                    realtimeSamplerCS.SetMatrixArray(Properties._AdditionalLightsWorldToShadow, additionalPass.additionalLightShadowSliceIndexTo_WorldShadowMatrix);
                }
            }

            if (renderer is UniversalRenderer urpRenderer)
            {
                hasMainLight = false;

                int mainLightIdx = renderingData.lightData.mainLightIndex;
                if (mainLightIdx >= 0)
                {
                    VisibleLight mainVisibleLight = renderingData.lightData.visibleLights[mainLightIdx];
                    Light mainLight = mainVisibleLight.light;
                    UniversalAdditionalLightData mainLightData = mainLight.GetUniversalAdditionalLightData();
                    
                    hasMainLight = mainLightData.volumetricsEnabled;

                    if (hasMainLight)
                    {
                        realtimeSamplerCS.SetVector(Properties._MainLightPosition, mainLight.transform.forward);

                        if (!mainLightData.volumetricsSyncIntensity)
                            realtimeSamplerCS.SetVector(Properties._MainLightColor, (mainVisibleLight.finalColor / mainLight.intensity) * mainLightData.volumetricsIntensity);
                        else
                            realtimeSamplerCS.SetVector(Properties._MainLightColor, mainVisibleLight.finalColor);

                        realtimeSamplerCS.SetVector(Properties._MainLightShadowParams, mainLight.shadows != LightShadows.None ? Vector4.one : Vector4.zero);
                    }
                }

                // We have two actuals.. 
                // urpActual is the URP light iter
                // volActual is the VOL light iter
                int urpActual = 0;
                int volActual = 0;
                for (int l = 0; l < renderingData.lightData.visibleLights.Length; l++)
                {
                    if (l == mainLightIdx)
                        continue;

                    VisibleLight visibleLight = renderingData.lightData.visibleLights[l];
                    Light light = visibleLight.light;
                    UniversalAdditionalLightData lightData = light.GetUniversalAdditionalLightData();

                    if (lightData.volumetricsEnabled)
                    {
                        additionalLightsFogIndices[volActual] = new Vector4(urpActual, 0, 0, 0);
                        //Debug.Log($"VOL {volActual} -> URP {urpActual}");

                        if (!lightData.volumetricsSyncIntensity)
                            additionalLightsColor[urpActual] = (light.color / light.intensity) * lightData.volumetricsIntensity;
                        else
                            additionalLightsColor[urpActual] = light.color;

                        volActual++;
                    }

                    urpActual++;
                }

                //Debug.Log($"URP = {renderingData.lightData.visibleLights.Length}; VOL = {actual}");

/*
                for (int a = 0; a < actual; a++) {
                    int index = (int)additionalLightsFogIndices[a].x;
                    Debug.Log(urpRenderer.ForwardLights.m_AdditionalLightPositions[index]);
                    Debug.Log(additionalLightsColor[index]);
                }
*/

                realtimeSamplerCS.SetVectorArray(Properties._AdditionalLightsPosition, urpRenderer.ForwardLights.m_AdditionalLightPositions);
                realtimeSamplerCS.SetVectorArray(Properties._AdditionalLightsColor, additionalLightsColor);
                realtimeSamplerCS.SetVectorArray(Properties._AdditionalLightsAttenuation, urpRenderer.ForwardLights.m_AdditionalLightAttenuations);
                realtimeSamplerCS.SetVectorArray(Properties._AdditionalLightsSpotDir, urpRenderer.ForwardLights.m_AdditionalLightSpotDirections);
                realtimeSamplerCS.SetVectorArray(Properties._AdditionalLightsFogIndices, additionalLightsFogIndices);
                realtimeSamplerCS.SetVector(Properties._AdditionalLightsCount, new Vector4(volActual, 0, 0, 0));
            }
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var additionalCameraData = renderingData.cameraData.camera.GetUniversalAdditionalCameraData();

            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;

            //desc.width = Mathf.CeilToInt((float)desc.width * additionalCameraData.volumetricsPercent);
            //desc.height = Mathf.CeilToInt((float)desc.height * additionalCameraData.volumetricsPercent);
            //desc.volumeDepth = (int)additionalCameraData.volumetricsQuality;

            desc.width = (int)additionalCameraData.volumetricsResolution;
            desc.height = (int)additionalCameraData.volumetricsResolution;
            desc.volumeDepth = (int)additionalCameraData.volumetricsQuality;

            // We need our own specific buffer
            desc.colorFormat = RenderTextureFormat.DefaultHDR;
            desc.depthBufferBits = 0;
            desc.dimension = TextureDimension.Tex3D;
            desc.enableRandomWrite = true;
            desc.useDynamicScale = false;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.msaaSamples = 1;

            cmd.GetTemporaryRT(FOG_TEX_ID, desc, FilterMode.Bilinear);
            fogIdent = new RenderTargetIdentifier(FOG_TEX_ID);

            cmd.GetTemporaryRT(FOG_COMPOSITE_TEX_ID, desc, FilterMode.Bilinear);
            fogCompositeIdent = new RenderTargetIdentifier(FOG_COMPOSITE_TEX_ID);

            depthMSAA = renderingData.cameraData.cameraTargetDescriptor.msaaSamples;

            fogWidth = desc.width;
            fogHeight = desc.height;
            fogDepth = desc.volumeDepth;
        }

        protected int GetGroups(float x, int threads) => Mathf.CeilToInt(x / threads);
        protected int GetGroups(float x, uint threads) => GetGroups(x, (int)threads);

        public void GetMatrices(ref RenderingData renderingData, out Matrix4x4[] cameraToWorld, out Matrix4x4[] worldToCamera, out Matrix4x4[] projection, out Matrix4x4[] inverseProjection)
        {
            Camera camera = renderingData.cameraData.camera;
            bool isXr = renderingData.cameraData.xrRendering;

            if (isXr)
            {
                var viewMatrix = camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
                var projectionMatrix = camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);

                worldToCamera = new Matrix4x4[2]
                {
                    camera.GetStereoViewMatrix(Camera.StereoscopicEye.Left),
                    camera.GetStereoViewMatrix(Camera.StereoscopicEye.Right)
                };

                cameraToWorld = new Matrix4x4[2]
                {
                    worldToCamera[0].inverse,
                    worldToCamera[1].inverse
                };

                projection = new Matrix4x4[2]
                {
                    GL.GetGPUProjectionMatrix(camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left), true).inverse,
                    GL.GetGPUProjectionMatrix(camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right), true).inverse,
                };

                inverseProjection = new Matrix4x4[2]
                {
                    camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left).inverse,
                    camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right).inverse
                };
            }
            else
            {
                cameraToWorld = new Matrix4x4[2] 
                { 
                    camera.cameraToWorldMatrix, 
                    Matrix4x4.identity 
                };

                worldToCamera = new Matrix4x4[2] 
                { 
                    camera.worldToCameraMatrix, 
                    Matrix4x4.identity 
                };

                projection = new Matrix4x4[2] 
                { 
                    GL.GetGPUProjectionMatrix(camera.projectionMatrix, true).inverse, 
                    Matrix4x4.identity 
                };

                inverseProjection = new Matrix4x4[2] 
                { 
                    camera.projectionMatrix.inverse, 
                    Matrix4x4.identity 
                };
            }

            // Column correction
            for (int m = 0; m < 2; m++)
                projection[m].m11 *= -1;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var additionalCameraData = renderingData.cameraData.camera.GetUniversalAdditionalCameraData();
            var renderer = renderingData.cameraData.renderer as UniversalRenderer;

            CommandBuffer cmd = CommandBufferPool.Get("RealtimeVolumetricsPass");
            cmd.Clear();

            uint thX, thY, thZ;

            bool isXr = renderingData.cameraData.xrRendering;

            float realtimeDensity = 1;
            float bakedDensity = 1;

            if (profile != null)
            {
                realtimeDensity = profile.realtimeDensity.value;
                bakedDensity = profile.bakedDensity.value;
            }

            GetMatrices(ref renderingData, out Matrix4x4[] cameraToWorld, out Matrix4x4[] worldToCamera, out Matrix4x4[] projection, out Matrix4x4[] inverseProjection);

            //
            // Clearing
            //
            int clearKernel = compositorCS.FindKernel("VolumetricClear");
            compositorCS.GetKernelThreadGroupSizes(clearKernel, out thX, out thY, out thZ);

            cmd.SetComputeTextureParam(compositorCS, clearKernel, Properties._Result, fogIdent);
            cmd.DispatchCompute(compositorCS, clearKernel, GetGroups(fogWidth, thX), GetGroups(fogHeight, thY), GetGroups(fogDepth, thZ));

            //
            // Realtime volumetrics
            //
            if (additionalCameraData.volumetricsRenderFlags.HasFlag(RenderVolumetrics.RenderFlags.Realtime))
            {
                cmd.SetComputeMatrixArrayParam(realtimeSamplerCS, Properties._CameraToWorld, cameraToWorld);
                cmd.SetComputeMatrixArrayParam(realtimeSamplerCS, Properties._WorldToCamera, worldToCamera);
                cmd.SetComputeMatrixArrayParam(realtimeSamplerCS, Properties._Projection, projection);
                cmd.SetComputeMatrixArrayParam(realtimeSamplerCS, Properties._InverseProjection, inverseProjection);

                cmd.SetComputeVectorParam(realtimeSamplerCS, Properties._FogParams, new Vector4(
                    fogDepth,
                    additionalCameraData.volumetricsFar,
                    realtimeDensity,
                    0
                ));

                cmd.SetComputeVectorParam(realtimeSamplerCS, Properties._PassData, new Vector4(fogWidth, fogHeight, fogDepth, 0));

                if (hasMainLight)
                {
                    cmd.SetComputeTextureParam(realtimeSamplerCS, realtimeInfo.mainKernel, Properties._Result, fogIdent);
                    cmd.DispatchCompute(realtimeSamplerCS, realtimeInfo.mainKernel,
                        GetGroups(fogWidth, realtimeInfo.mainThX),
                        GetGroups(fogHeight, realtimeInfo.mainThY),
                        GetGroups(fogDepth, realtimeInfo.mainThZ)
                    );
                }

                cmd.SetComputeTextureParam(realtimeSamplerCS, realtimeInfo.additionalKernel, Properties._Result, fogIdent);
                cmd.DispatchCompute(realtimeSamplerCS, realtimeInfo.additionalKernel,
                    GetGroups(fogWidth, realtimeInfo.additionalThX),
                    GetGroups(fogHeight, realtimeInfo.additionalThY),
                    GetGroups(fogDepth, realtimeInfo.additionalThZ)
                );
            }

            //
            // Baked sampling
            //
            if (additionalCameraData.volumetricsRenderFlags.HasFlag(RenderVolumetrics.RenderFlags.Baked))
            {
                cmd.SetComputeMatrixArrayParam(bakedSamplerCS, Properties._CameraToWorld, cameraToWorld);
                cmd.SetComputeMatrixArrayParam(bakedSamplerCS, Properties._WorldToCamera, worldToCamera);
                cmd.SetComputeMatrixArrayParam(bakedSamplerCS, Properties._Projection, projection);
                cmd.SetComputeMatrixArrayParam(bakedSamplerCS, Properties._InverseProjection, inverseProjection);

                cmd.SetComputeVectorParam(bakedSamplerCS, Properties._FogParams, new Vector4(
                    fogDepth,
                    additionalCameraData.volumetricsFar,
                    bakedDensity,
                    0
                ));

                cmd.SetComputeVectorParam(bakedSamplerCS, Properties._PassData, new Vector4(fogWidth, fogHeight, fogDepth, 0));

                cmd.SetComputeTextureParam(bakedSamplerCS, bakedInfo.bakeKernel, Properties._NoisePattern, noisePattern);
                cmd.SetComputeTextureParam(bakedSamplerCS, bakedInfo.bakeKernel, Properties._Result, fogIdent);

                foreach (BakedVolumeProbe volume in BakedVolumeProbe.bakedVolumes)
                {
                    if (volume == null)
                        continue;

                    if (volume.buffer == null)
                        continue;

                    cmd.SetComputeMatrixParam(bakedSamplerCS, Properties._BakeMatrixInverse, volume.transform.worldToLocalMatrix);
                    cmd.SetComputeVectorParam(bakedSamplerCS, Properties._BakeOrigin, volume.bounds.center);
                    cmd.SetComputeVectorParam(bakedSamplerCS, Properties._BakeExtents, volume.bounds.extents);

                    cmd.SetComputeTextureParam(bakedSamplerCS, bakedInfo.bakeKernel, Properties._MainTex, volume.buffer);
                    cmd.DispatchCompute(bakedSamplerCS, bakedInfo.bakeKernel,
                        GetGroups(fogWidth, bakedInfo.bakeThX),
                        GetGroups(fogHeight, bakedInfo.bakeThY),
                        GetGroups(fogDepth, bakedInfo.bakeThZ)
                    );
                }
            }

            //
            // Compositor step
            //
            int compositorKernel = compositorCS.FindKernel("CSMain");

            cmd.SetComputeTextureParam(compositorCS, compositorKernel, Properties._Result, fogCompositeIdent);           
            cmd.SetComputeTextureParam(compositorCS, compositorKernel, Properties._MainTex, fogIdent);

            cmd.SetComputeTextureParam(compositorCS, compositorKernel, Properties._SceneDepth, renderingData.cameraData.renderer.cameraDepthTarget);

            LocalKeyword lowQuality = new LocalKeyword(compositorCS, "QUALITY_LOW");
            LocalKeyword mediumQuality = new LocalKeyword(compositorCS, "QUALITY_MEDIUM");
            LocalKeyword highQuality = new LocalKeyword(compositorCS, "QUALITY_HIGH");
            LocalKeyword ultraQuality = new LocalKeyword(compositorCS, "QUALITY_ULTRA");
            LocalKeyword overkillQuality = new LocalKeyword(compositorCS, "QUALITY_OVERKILL");

            switch (additionalCameraData.volumetricsQuality)
            {
                default:
                    cmd.DisableKeyword(compositorCS, lowQuality);
                    cmd.DisableKeyword(compositorCS, mediumQuality);
                    cmd.DisableKeyword(compositorCS, highQuality);
                    cmd.DisableKeyword(compositorCS, ultraQuality);
                    cmd.DisableKeyword(compositorCS, overkillQuality);
                    break;

                case RenderVolumetrics.BufferQuality.Low:
                    cmd.EnableKeyword(compositorCS, lowQuality);
                    cmd.DisableKeyword(compositorCS, mediumQuality);
                    cmd.DisableKeyword(compositorCS, highQuality);
                    cmd.DisableKeyword(compositorCS, ultraQuality);
                    cmd.DisableKeyword(compositorCS, overkillQuality);
                    break;

                case RenderVolumetrics.BufferQuality.Medium:
                    cmd.DisableKeyword(compositorCS, lowQuality);
                    cmd.EnableKeyword(compositorCS, mediumQuality);
                    cmd.DisableKeyword(compositorCS, highQuality);
                    cmd.DisableKeyword(compositorCS, ultraQuality);
                    cmd.DisableKeyword(compositorCS, overkillQuality);
                    break;

                case RenderVolumetrics.BufferQuality.High:
                    cmd.DisableKeyword(compositorCS, lowQuality);
                    cmd.DisableKeyword(compositorCS, mediumQuality);
                    cmd.EnableKeyword(compositorCS, highQuality);
                    cmd.DisableKeyword(compositorCS, ultraQuality);
                    cmd.DisableKeyword(compositorCS, overkillQuality);
                    break;

                case RenderVolumetrics.BufferQuality.Ultra:
                    cmd.DisableKeyword(compositorCS, lowQuality);
                    cmd.DisableKeyword(compositorCS, mediumQuality);
                    cmd.DisableKeyword(compositorCS, highQuality);
                    cmd.EnableKeyword(compositorCS, ultraQuality);
                    cmd.DisableKeyword(compositorCS, overkillQuality);
                    break;

                case RenderVolumetrics.BufferQuality.Overkill:
                    cmd.DisableKeyword(compositorCS, lowQuality);
                    cmd.DisableKeyword(compositorCS, mediumQuality);
                    cmd.DisableKeyword(compositorCS, highQuality);
                    cmd.DisableKeyword(compositorCS, ultraQuality);
                    cmd.EnableKeyword(compositorCS, overkillQuality);
                    break;
            }

            compositorCS.GetKernelThreadGroupSizes(compositorKernel, out thX, out thY, out thZ);
            cmd.DispatchCompute(compositorCS, compositorKernel, GetGroups(fogWidth, thX), GetGroups(fogHeight, thY), 1);

            //
            // Blending step
            //
            switch (depthMSAA)
            {
                default:
                    cmd.DisableShaderKeyword("_DEPTH_MSAA_2");
                    cmd.DisableShaderKeyword("_DEPTH_MSAA_4");
                    cmd.DisableShaderKeyword("_DEPTH_MSAA_8");
                    break;

                case 2:
                    cmd.EnableShaderKeyword("_DEPTH_MSAA_2");
                    cmd.DisableShaderKeyword("_DEPTH_MSAA_4");
                    cmd.DisableShaderKeyword("_DEPTH_MSAA_8");
                    break;

                case 4:
                    cmd.DisableShaderKeyword("_DEPTH_MSAA_2");
                    cmd.EnableShaderKeyword("_DEPTH_MSAA_4");
                    cmd.DisableShaderKeyword("_DEPTH_MSAA_8");
                    break;

                case 8:
                    cmd.DisableShaderKeyword("_DEPTH_MSAA_2");
                    cmd.DisableShaderKeyword("_DEPTH_MSAA_4");
                    cmd.EnableShaderKeyword("_DEPTH_MSAA_8");
                    break;
            }

            float noiseScaleX = (float)noisePattern.width / (float)renderingData.cameraData.pixelWidth;
            float noiseScaleY = (float)noisePattern.height / (float)renderingData.cameraData.pixelHeight;

            cmd.SetGlobalTexture(Properties._MainTex, fogCompositeIdent);
            cmd.SetGlobalTexture(Properties._SceneDepth, renderingData.cameraData.renderer.cameraDepthTarget);
            cmd.SetGlobalTexture(Properties._NoisePattern, noisePattern);
            cmd.SetGlobalVector(Properties._NoiseData, new Vector4(1.0F / renderingData.cameraData.pixelWidth, 1.0F / renderingData.cameraData.pixelHeight, fogDepth, Time.time));
            cmd.SetGlobalVector(Properties._PassData, new Vector4(noiseScaleX, noiseScaleY, additionalCameraData.volumetricsFar, 0));
            cmd.SetRenderTarget(new RenderTargetIdentifier(renderingData.cameraData.renderer.cameraColorTarget, 0, CubemapFace.Unknown, -1));
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, blendPS, 0, 0);

            // If we're in VR we need to repeat this whole process once again for the other eye!
            if (isXr)
            {
                compositorCS.GetKernelThreadGroupSizes(clearKernel, out thX, out thY, out thZ);
                cmd.DispatchCompute(compositorCS, clearKernel, GetGroups(fogWidth, thX), GetGroups(fogHeight, thY), GetGroups(fogDepth, thZ));

                if (additionalCameraData.volumetricsRenderFlags.HasFlag(RenderVolumetrics.RenderFlags.Realtime))
                {
                    cmd.SetComputeVectorParam(realtimeSamplerCS, Properties._PassData, new Vector4(fogWidth, fogHeight, fogDepth, 1));

                    if (hasMainLight)
                    {
                        cmd.SetComputeTextureParam(realtimeSamplerCS, realtimeInfo.mainKernel, Properties._Result, fogIdent);
                        cmd.DispatchCompute(realtimeSamplerCS, realtimeInfo.mainKernel,
                            GetGroups(fogWidth, realtimeInfo.mainThX),
                            GetGroups(fogHeight, realtimeInfo.mainThY),
                            GetGroups(fogDepth, realtimeInfo.mainThZ)
                        );
                    }

                    cmd.SetComputeTextureParam(realtimeSamplerCS, realtimeInfo.additionalKernel, Properties._Result, fogIdent);
                    cmd.DispatchCompute(realtimeSamplerCS, realtimeInfo.additionalKernel,
                        GetGroups(fogWidth, realtimeInfo.additionalThX),
                        GetGroups(fogHeight, realtimeInfo.additionalThY),
                        GetGroups(fogDepth, realtimeInfo.additionalThZ)
                    );
                }

                if (additionalCameraData.volumetricsRenderFlags.HasFlag(RenderVolumetrics.RenderFlags.Baked))
                {
                    cmd.SetComputeVectorParam(bakedSamplerCS, Properties._PassData, new Vector4(fogWidth, fogHeight, fogDepth, 1));

                    foreach (BakedVolumeProbe volume in BakedVolumeProbe.bakedVolumes)
                    {
                        if (volume == null)
                            continue;

                        if (volume.buffer == null)
                            continue;

                        cmd.SetComputeMatrixParam(bakedSamplerCS, Properties._BakeMatrixInverse, volume.transform.worldToLocalMatrix);
                        cmd.SetComputeVectorParam(bakedSamplerCS, Properties._BakeOrigin, volume.bounds.center);
                        cmd.SetComputeVectorParam(bakedSamplerCS, Properties._BakeExtents, volume.bounds.extents);

                        cmd.SetComputeTextureParam(bakedSamplerCS, bakedInfo.bakeKernel, Properties._MainTex, volume.buffer);
                        cmd.DispatchCompute(bakedSamplerCS, bakedInfo.bakeKernel,
                            GetGroups(fogWidth, bakedInfo.bakeThX),
                            GetGroups(fogHeight, bakedInfo.bakeThY),
                            GetGroups(fogDepth, bakedInfo.bakeThZ)
                        );
                    }
                }

                compositorCS.GetKernelThreadGroupSizes(compositorKernel, out thX, out thY, out thZ);
                cmd.DispatchCompute(compositorCS, compositorKernel, GetGroups(fogWidth, thX), GetGroups(fogHeight, thY), 1);

                cmd.SetGlobalVector(Properties._PassData, new Vector4(noiseScaleX, noiseScaleY, additionalCameraData.volumetricsFar, 1));
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, blendPS, 0, 0);
            } 

            cmd.ReleaseTemporaryRT(FOG_TEX_ID);
            cmd.ReleaseTemporaryRT(FOG_COMPOSITE_TEX_ID);

            context.ExecuteCommandBuffer(cmd);

            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }
}
