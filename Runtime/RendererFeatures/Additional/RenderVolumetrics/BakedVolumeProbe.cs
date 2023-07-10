using System.Collections;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Rendering.Universal.Additions
{
    [ExecuteAlways]
    public class BakedVolumeProbe : MonoBehaviour
    {
        public static List<BakedVolumeProbe> bakedVolumes = new List<BakedVolumeProbe>();

        [System.Flags]
        public enum PassFlags 
        {
            None = 0,

            Direct = 1,
            Indirect = 2,

            All = ~0
        }

        public enum SH9SampleShape
        {
            Octagonal,
            Spherical,
        }

        public enum SH9Process
        {
            Average,
            UseMax,
            UseMin
        }

        public enum SH9Color
        {
            Linear,
            Gamma
        }

        [Header("Shape")]
        public Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

        [Min(0)]
        [Tooltip("What is the pixel width of the buffer?")]
        public int bufferWidth = 16;
        [Min(0)]
        [Tooltip("What is the pixel height of the buffer?")]
        public int bufferHeight = 16;
        [Min(0)]
        [Tooltip("What is the pixel depth of the buffer?")]
        public int bufferDepth = 16;

        [Header("Bake Settings")]
        [Tooltip("What passes are rendered into the bake?")]
        public PassFlags passFlags = PassFlags.All;

        [Header("Indirect Sampling")]
        [Tooltip("What shape do we sample indirect info in? (Octagonal = Left, Right, Up, Down, Forward, Back / OctagonalCorners = Rotated octagonal")]
        public SH9SampleShape sampleShape = SH9SampleShape.Octagonal;

        [Tooltip("How do we interpret the data we've sampled?")]
        public SH9Process sampleProcess = SH9Process.Average;

        [Tooltip("What colorspace is the data?")]
        public SH9Color sampleColor = SH9Color.Linear;

        [Tooltip("Multiplies the final output")]
        [Min(0)]
        public float density = 1;

        [ColorUsage(false, true)]
        [Tooltip("An HDR color to filter the output by")]
        public Color filter = Color.white;

        [Header("Behavior")]
        public bool bakeAfterLightmapBake = true;

        [Header("Visualization")]
        public Vector3 visualizerOffset;
        public float visualizerSize = 0.5F;

        [Header("Result")]
        public Texture3D buffer;

        protected ComputeShader bakeCS;

        public void OnEnable()
        {
            bakeCS = Resources.Load<ComputeShader>("VolumetricsBaker");

            bakedVolumes.Add(this);

#if UNITY_EDITOR
            if (bakeAfterLightmapBake)
                Lightmapping.bakeCompleted += BakeVolume;
#endif
        }

        public void OnDisable()
        {
            bakedVolumes.Remove(this);

#if UNITY_EDITOR
            if (bakeAfterLightmapBake)
                Lightmapping.bakeCompleted -= BakeVolume;
#endif
        }

#if UNITY_EDITOR
        public Vector3[] GetSampleDirections()
        {
            switch (sampleShape)
            {
                default:
                    return new Vector3[] { Vector3.forward };

                case SH9SampleShape.Octagonal:
                    return new Vector3[] { Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left };

                case SH9SampleShape.Spherical:
                    return new Vector3[]
                    {
                        Vector3.forward,
                        Vector3.back,
                        Vector3.right,
                        Vector3.left,
                        Vector3.up,
                        Vector3.down,

                        (Vector3.forward + Vector3.right).normalized,
                        (Vector3.forward + Vector3.left).normalized,
                        (Vector3.forward + Vector3.up).normalized,
                        (Vector3.forward + Vector3.down).normalized,
                        (Vector3.back + Vector3.right).normalized,
                        (Vector3.back + Vector3.left).normalized,
                        (Vector3.back + Vector3.up).normalized,
                        (Vector3.back + Vector3.down).normalized,

                        (Vector3.forward + Vector3.right + Vector3.up).normalized,
                        (Vector3.forward + Vector3.left + Vector3.up).normalized,
                        (Vector3.forward + Vector3.right + Vector3.down).normalized,
                        (Vector3.forward + Vector3.left + Vector3.down).normalized,

                        (Vector3.back + Vector3.right + Vector3.up).normalized,
                        (Vector3.back + Vector3.left + Vector3.up).normalized,
                        (Vector3.back + Vector3.right + Vector3.down).normalized,
                        (Vector3.back + Vector3.left + Vector3.down).normalized,
                    };
            }
        }

        public Color ProcessColor(Color[] colors)
        {
            Color color = Color.black;

            switch (sampleProcess)
            {
                default:
                    Color average = new Color(0, 0, 0, 0);

                    for (int c = 0; c < colors.Length; c++)
                        average += colors[c];

                    color = average / colors.Length;
                    break;

                case SH9Process.UseMax:
                    Color greatest = new Color(0, 0, 0, 0);
                    float brightest = 0;

                    for (int c = 0; c < colors.Length; c++)
                        if (colors[c].grayscale > brightest)
                        {
                            greatest = colors[c];
                            brightest = greatest.grayscale;
                        }

                    color = greatest;
                    break;

                case SH9Process.UseMin:
                    Color lowest = new Color(0, 0, 0, 0);
                    float dimmest = 1;

                    for (int c = 0; c < colors.Length; c++)
                        if (colors[c].grayscale < dimmest)
                        {
                            lowest = colors[c];
                            dimmest = lowest.grayscale;
                        }

                    color = lowest;
                    break;
            }

            color *= density * filter;
            color.a = 1;

            if (sampleColor == SH9Color.Linear)
                return color;
            else
                return color.gamma;
        }

        public Vector3 GetPointInBounds(int x, int y, int z)
        {
            float xDelta = (float)x / ((float)bufferWidth - 1);
            float yDelta = (float)y / ((float)bufferHeight - 1);
            float zDelta = (float)z / ((float)bufferDepth - 1);

            Vector3 delta = new Vector3(xDelta, yDelta, zDelta);

            Vector3 interior = Vector3.Scale(bounds.size, delta) - bounds.extents;
            Vector3 point = bounds.center + interior;
            return transform.TransformPoint(point);
        }

        void BakeDirect(ref Texture3D texture)
        {
            List<Light> bakedLights = new List<Light>();
            List<Renderer> staticRenderers = new List<Renderer>();

            foreach (Light light in Object.FindObjectsOfType<Light>())
            {
                if (!light.enabled || !light.gameObject.activeInHierarchy)
                    continue;

                var additionalData = light.GetUniversalAdditionalLightData();

                if (additionalData.volumetricsEnabled && light.lightmapBakeType == LightmapBakeType.Baked)
                    bakedLights.Add(light);
            }

            /*
            foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>())
            {
                if (renderer.gameObject.isStatic || renderer.staticShadowCaster)
                    staticRenderers.Add(renderer);
            }
            */

            // For each light we need to render the shadows, then process the light inside the volume w/ shadowing
            //RenderTexture shadowBuffer = new RenderTexture(1024, 1024, 32, RenderTextureFormat.RFloat);
            //shadowBuffer.Create();

            RenderTexture volumeBuffer = new RenderTexture(bufferWidth, bufferHeight, 0, RenderTextureFormat.ARGBHalf);
            volumeBuffer.enableRandomWrite = true;
            volumeBuffer.Create();

            //CommandBuffer buffer = CommandBufferPool.Get("BAKED VOLUME SHADOWS");

            foreach (Light light in bakedLights)
            {
                //buffer.Clear();

                //buffer.SetViewport(new Rect(0, 0, shadowBuffer.width, shadowBuffer.height));
                //buffer.SetRenderTarget(shadowBuffer);

                Vector3 origin = light.transform.position;
                Vector3 forward = light.transform.forward;
                Vector3 up = light.transform.up;

                Matrix4x4 look = Matrix4x4.LookAt(origin, origin + forward, -up);
                Matrix4x4 view = Matrix4x4.LookAt(origin, origin - forward, up).inverse;
                Matrix4x4 proj = Matrix4x4.Perspective(light.spotAngle, 1, 0.01F, 100F);

                /*
                buffer.SetViewProjectionMatrices(view, proj);

                foreach (Renderer renderer in staticRenderers)
                    buffer.DrawRenderer(renderer, shadowCasterMaterial);

                Graphics.ExecuteCommandBuffer(buffer);
                */

                UniversalRenderPipeline.GetLightAttenuationAndSpotDirection(light.type, light.range, look, light.spotAngle, light.innerSpotAngle, out Vector4 atten, out Vector4 dir);

                int kernel = bakeCS.FindKernel("CSMain");
                bakeCS.GetKernelThreadGroupSizes(kernel, out uint xTileSize, out uint yTileSize, out uint zTileSize);

                int tilesX = Mathf.CeilToInt(bufferWidth / (float)xTileSize);
                int tilesY = Mathf.CeilToInt(bufferDepth / (float)yTileSize);
                bakeCS.SetTexture(kernel, "_Result", volumeBuffer);

                bakeCS.SetMatrix("_BakeMatrix", Matrix4x4.TRS(transform.position + bounds.center, transform.rotation, bounds.extents));

                bakeCS.SetVector("_LightAtten", atten);
                bakeCS.SetVector("_LightDir", dir);
                bakeCS.SetVector("_LightPos", new Vector4(origin.x, origin.y, origin.z, 1.0F));

                for (int s = 0; s < bufferDepth; s++) 
                {
                    bakeCS.SetVector("_SliceData", new Vector4(s, bufferHeight));
                    bakeCS.Dispatch(kernel, tilesX, tilesY, 1);

                    RenderTexture.active = volumeBuffer;
                    Texture2D dupe = new Texture2D(volumeBuffer.width, volumeBuffer.height, TextureFormat.RGBAHalf, false);
                    dupe.ReadPixels(new Rect(0, 0, dupe.width, dupe.height), 0, 0);

                    for (int x = 0; x < bufferWidth; x++)
                        for (int y = 0; y < bufferDepth; y++)
                        {
                            Color color = dupe.GetPixel(x, y) * light.color * light.intensity + texture.GetPixel(x, y, s);
                            texture.SetPixel(x, y, s, color);
                        }

                    texture.Apply();
                }
            }

            RenderTexture.active = null;

            //shadowBuffer.Release();
            volumeBuffer.Release();
        }

        // CPU only!
        // This uses the SH9 probes inside of Unity!
        void BakeIndirect(ref Texture3D texture)
        {
            Vector3[] directions = GetSampleDirections();
            Color[] colors = new Color[directions.Length];

            for (int x = 0; x < bufferWidth; x++)
            {
                for (int y = 0; y < bufferHeight; y++)
                {
                    for (int z = 0; z < bufferDepth; z++)
                    {
                        Vector3 point = GetPointInBounds(x, y, z);
                        LightProbes.GetInterpolatedProbe(point, null, out SphericalHarmonicsL2 probe);

                        probe.Evaluate(directions, colors);

                        texture.SetPixel(x, y, z, ProcessColor(colors));
                    }
                }
            }

            texture.Apply();
        }

        Color SaturateColor(Color color) => new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b), Mathf.Clamp01(color.a));

        [ContextMenu("Bake Volume")]
        public void BakeVolume()
        {
            Debug.Log("Baking volumetrics!");

            Texture3D texture = new Texture3D(bufferWidth, bufferHeight, bufferDepth, TextureFormat.RGBAHalf, true);
            texture.name = "BakedVolumeResult";

            for (int x = 0; x < bufferWidth; x++)
                for (int y = 0; y < bufferHeight; y++)
                    for (int z = 0; z < bufferDepth; z++)
                        texture.SetPixel(x, y, z, new Color(0, 0, 0, 1));

            texture.Apply();

            if (passFlags.HasFlag(PassFlags.Indirect))
                BakeIndirect(ref texture);

            if (passFlags.HasFlag(PassFlags.Direct))
                BakeDirect(ref texture);

            /*
            for (int x = 0; x < bufferWidth; x++)
                for (int y = 0; y < bufferHeight; y++)
                    for (int z = 0; z < bufferDepth; z++)
                        texture.SetPixel(x, y, z, SaturateColor(texture.GetPixel(x, y, z)));

            texture.Apply();
            */

            buffer = texture;
        }

        public void OnDrawGizmosSelected()
        {
            Handles.matrix = Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            Vector3 point = transform.TransformPoint(visualizerOffset + bounds.center);
            LightProbes.GetInterpolatedProbe(point, null, out SphericalHarmonicsL2 probe);

            Gizmos.color = Color.white;
            List<Color> colors = new List<Color>();

            foreach (Vector3 direction in GetSampleDirections())
            {
                Color[] color = new Color[1];
                probe.Evaluate(new Vector3[] { direction }, color);

                Handles.color = color[0];
                Handles.ArrowHandleCap(0, visualizerOffset + bounds.center, Quaternion.LookRotation(direction), visualizerSize, EventType.Repaint);

                colors.Add(color[0]);
            }

            Gizmos.color = ProcessColor(colors.ToArray());
            Gizmos.DrawSphere(visualizerOffset + bounds.center, 0.1F);
        }
#endif
    }
}
