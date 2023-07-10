using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Rendering.Universal.Additions
{
    [Serializable, VolumeComponentMenu("URP Additions/Volumetrics")]
    public sealed class RenderVolumetricsProfile : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("The strength of realtime sampled fog.")]
        public MinFloatParameter realtimeDensity = new MinFloatParameter(1F, 0F);

        [Tooltip("The strength of baked sampled fog.")]
        public MinFloatParameter bakedDensity = new MinFloatParameter(1F, 0F);

        /// <summary>
        /// Is the component active?
        /// </summary>
        /// <returns>True is the component is active</returns>
        public bool IsActive() => realtimeDensity.value > 0F && bakedDensity .value > 0F;

        /// <summary>
        /// Is the component compatible with on tile rendering
        /// </summary>
        /// <returns>false</returns>
        public bool IsTileCompatible() => false;
    }
}
