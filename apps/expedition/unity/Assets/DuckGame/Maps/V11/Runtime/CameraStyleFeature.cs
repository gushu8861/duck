using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuckGame.Maps.V11
{
    public sealed class CameraStyleFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;
        private Material material;
        private StylePass pass;

        public override void Create()
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
            material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
            pass = material != null ? new StylePass(material) : null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            if (pass == null || camera.cameraType != CameraType.Game ||
                !camera.TryGetComponent<CameraStyle>(out var style) || !style.isActiveAndEnabled ||
                style.Current == CameraStyle.Look.Cartoon)
                return;

            pass.Look = style.Current;
            pass.Strength = style.Strength;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
        }

        private sealed class StylePass : ScriptableRenderPass
        {
            private static readonly int LookId = Shader.PropertyToID("_Look");
            private static readonly int StrengthId = Shader.PropertyToID("_Strength");
            private readonly Material material;
            private RTHandle scratch;

            public CameraStyle.Look Look { get; set; }
            public float Strength { get; set; }

            public StylePass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Color);
            }

            public override void OnCameraSetup(CommandBuffer command, ref RenderingData renderingData)
            {
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(ref scratch, descriptor, FilterMode.Point, TextureWrapMode.Clamp,
                    name: "_V11CameraStyle");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var command = CommandBufferPool.Get("V11 Camera Style");
                try
                {
                    material.SetFloat(LookId, (float)Look);
                    material.SetFloat(StrengthId, Strength);
                    var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                    Blitter.BlitCameraTexture(command, source, scratch, material, 0);
                    Blitter.BlitCameraTexture(command, scratch, source);
                    context.ExecuteCommandBuffer(command);
                }
                finally
                {
                    CommandBufferPool.Release(command);
                }
            }

            public void Dispose() => scratch?.Release();
        }
    }
}
