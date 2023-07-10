using System;

using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal.Additions
{
    public class RenderVolumetrics : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Header("If you're looking for other settings, they're on the lights and cameras!")]

            [Header("Realtime Lighting")]
            [Tooltip("If this is set, we use a different sampling shader for realtime lighting")]
            public ComputeShader realtimeSamplerCS;

            [Tooltip("What is the name of the realtime main light kernel?")]
            public string realtimeMainSamplerKernel = "MainLightSample";

            [Tooltip("What is the name of the realtime additional light kernel?")]
            public string realtimeAdditionalSamplerKernel = "AdditionalLightSample";

            [Header("Baked Lighting")]
            [Tooltip("If this is set, we use a different sampling shader for baked lighting")]
            public ComputeShader bakedSamplerCS;

            [Tooltip("What is the name of the baked light kernel?")]
            public string bakedSamplerKernel = "BakedSample";

            [Header("Presentation")]
            [Tooltip("If this is set, we use a different compositing shader")]
            public ComputeShader compositorCS;

            [Tooltip("If this is set, we use a different noise pattern texture")]
            public Texture2D noisePattern;

            [Tooltip("If this is set, we use a different blending shader")]
            public Shader blendShader;
        }

        public class RealtimePassInfo
        {
            public uint mainThX, mainThY, mainThZ;
            public int mainKernel;
            public int mainLightIndex;

            public uint additionalThX, additionalThY, additionalThZ;
            public int additionalKernel;
        }

        public class BakedPassInfo
        {
            public uint bakeThX, bakeThY, bakeThZ;
            public int bakeKernel;
        }

        public const int MAX_VISIBLE_LIGHTS = 256;

        [Header("EXPERIMENTAL and WIP!")]
        public Settings settings;

        RenderVolumetricsPass fogPass;

        public enum BufferQuality : int {
            VeryLow     = 16,
            Low         = 32,
            Medium      = 64,
            High        = 96,
            Ultra       = 128,
            Overkill    = 256,
        }

        [Flags]
        public enum RenderFlags
        {
            None = 0,

            Realtime = 1,
            Baked = 2,

            All = ~0
        }

        public override void Create()
        {
            fogPass = new RenderVolumetricsPass();
            fogPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public void UpdateRealtimeLights(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            RealtimePassInfo passInfo = new RealtimePassInfo();

            passInfo.mainKernel = fogPass.realtimeSamplerCS.FindKernel(settings.realtimeMainSamplerKernel);
            passInfo.additionalKernel = fogPass.realtimeSamplerCS.FindKernel(settings.realtimeAdditionalSamplerKernel);

            fogPass.realtimeSamplerCS.GetKernelThreadGroupSizes(passInfo.mainKernel, out passInfo.mainThX, out passInfo.mainThY, out passInfo.mainThZ);
            fogPass.realtimeSamplerCS.GetKernelThreadGroupSizes(passInfo.additionalKernel, out passInfo.additionalThX, out passInfo.additionalThY, out passInfo.additionalThZ);

            fogPass.realtimeInfo = passInfo;
        }

        public void UpdateBaked()
        {
            BakedPassInfo passInfo = new BakedPassInfo();

            passInfo.bakeKernel = fogPass.bakedSamplerCS.FindKernel(settings.bakedSamplerKernel);
            fogPass.bakedSamplerCS.GetKernelThreadGroupSizes(passInfo.bakeKernel, out passInfo.bakeThX, out passInfo.bakeThY, out passInfo.bakeThZ);

            fogPass.bakedInfo = passInfo;
        }

        public T DefaultVerifyFunc<T>(string fallback) where T : UnityEngine.Object => Resources.Load<T>(fallback);

        public bool VerifySetting<T>(ref T target, T resource, string fallback, Func<string, T> loadFunc) where T : UnityEngine.Object
        {
            return (target = resource == null ? loadFunc(fallback) : resource) != null;
        }

        public bool VerifySetting<T>(ref T target, T resource, string fallback) where T : UnityEngine.Object
        {
            return VerifySetting(ref target, resource, fallback, DefaultVerifyFunc<T>);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var additionalCameraData = renderingData.cameraData.camera.GetUniversalAdditionalCameraData();

            if (!additionalCameraData.renderVolumetrics)
                return;


            if (!VerifySetting(ref fogPass.realtimeSamplerCS, settings.realtimeSamplerCS, "RealtimeVolumetricSampler"))
                throw new Exception("Please assign a realtime sampler compute shader to the Volumetric Pass!");

            if (!VerifySetting(ref fogPass.bakedSamplerCS, settings.bakedSamplerCS, "BakedVolumetricSampler"))
                throw new Exception("Please assign a baked sampler compute shader to the Volumetric Pass!");

            if (!VerifySetting(ref fogPass.compositorCS, settings.compositorCS, "VolumetricCompositor"))
                throw new Exception("Please assign a compositor compute shader to the Volumetric Pass!");

            if (!VerifySetting(ref fogPass.noisePattern, settings.noisePattern, "VolumeNoisePattern"))
                throw new Exception("Please assign a noise pattern to the Volumetric Pass!");

            if (!fogPass.blendPS)
                fogPass.blendPS = new Material(settings.blendShader == null ? Shader.Find("Hidden/Volumetrics/FogBlend") : settings.blendShader);

            if (fogPass.blendPS == null)
                Debug.LogError("Please assign a blending shader to the Volumetric Pass!");


            var stack = VolumeManager.instance.stack;
            var volumetricsProfile = stack.GetComponent<RenderVolumetricsProfile>();

            fogPass.settings = settings;
            fogPass.profile = volumetricsProfile;

            UpdateRealtimeLights(renderer, ref renderingData);
            UpdateBaked();

            renderer.EnqueuePass(fogPass);
        }
    }
}