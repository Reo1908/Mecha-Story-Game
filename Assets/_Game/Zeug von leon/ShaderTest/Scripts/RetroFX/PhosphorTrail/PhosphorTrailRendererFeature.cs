using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace RetroFX
{
    /// <summary>
    /// Adds this to your URP Renderer asset's Renderer Features list.
    /// Reads settings from a PhosphorTrailVolume override on the active Volume stack.
    /// Maintains a small persistent history buffer that survives across
    /// frames - this is what makes it a genuine multi-frame trail rather
    /// than a same-frame directional blur.
    /// </summary>
    public class PhosphorTrailRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;
        [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        private Material material;
        private PhosphorTrailPass pass;

        private static class Passes
        {
            public const int Prefilter = 0;
            public const int Decay = 1;
            public const int Combine = 2;
            public const int Residual = 3;
            public const int Soften = 4;
            public const int Composite = 5;
        }

        public override void Create()
        {
            if (shader == null)
            {
                shader = Shader.Find("Hidden/RetroFX/PhosphorTrail");
            }
            if (shader != null)
            {
                material = CoreUtils.CreateEngineMaterial(shader);
            }
            pass = new PhosphorTrailPass(material) { renderPassEvent = renderPassEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null)
            {
                Debug.LogWarning("[RetroFX] PhosphorTrail: material is null - drag PhosphorTrail.shader onto the Shader field on this component manually.");
                return;
            }

            var stack = VolumeManager.instance.stack;
            var settings = stack.GetComponent<PhosphorTrailVolume>();
            if (settings == null || !settings.IsActive()) return;
            if (renderingData.cameraData.cameraType == CameraType.Preview) return;

            pass.Setup(settings);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            pass?.ReleaseHistory();
        }

        private class PhosphorTrailPass : ScriptableRenderPass
        {
            private readonly Material material;
            private PhosphorTrailVolume settings;
            private RTHandle m_History;

            private class PassData
            {
                public Material material;
                public TextureHandle source;
                public TextureHandle combineTex;
                public int passIndex;
                public Vector2 texelSize;
            }

            public PhosphorTrailPass(Material material)
            {
                this.material = material;
                profilingSampler = new ProfilingSampler("RetroFX Phosphor Trail");
                requiresIntermediateTexture = true;
            }

            public void Setup(PhosphorTrailVolume settings)
            {
                this.settings = settings;
            }

            public void ReleaseHistory()
            {
                m_History?.Release();
                m_History = null;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                if (resourceData.isActiveTargetBackBuffer) return;

                TextureHandle source = resourceData.activeColorTexture;

                material.SetFloat("_Threshold", settings.threshold.value);
                material.SetFloat("_Knee", Mathf.Max(settings.knee.value, 1e-4f));
                material.SetFloat("_Clamp", settings.clamp.value);
                material.SetColor("_Tint", settings.tint.value);
                material.SetFloat("_Intensity", settings.intensity.value);

                int downsample = Mathf.Max(1, settings.downsample.value);
                int width = Mathf.Max(4, cameraData.cameraTargetDescriptor.width / downsample);
                int height = Mathf.Max(4, cameraData.cameraTargetDescriptor.height / downsample);

                var baseDesc = renderGraph.GetTextureDesc(source);
                baseDesc.width = width;
                baseDesc.height = height;
                baseDesc.clearBuffer = false;
                baseDesc.msaaSamples = MSAASamples.None;

                // Persistent history buffer - reallocated only if resolution/format changed.
                var historyDesc = baseDesc;
                historyDesc.name = "_PhosphorTrail_History";
                RenderingUtils.ReAllocateHandleIfNeeded(ref m_History, historyDesc, name: "_PhosphorTrail_History");
                TextureHandle history = renderGraph.ImportTexture(m_History);

                // ---- Prefilter (threshold current frame + implicit downsample) ----
                var prefilterDesc = baseDesc;
                prefilterDesc.name = "_PhosphorTrail_Prefilter";
                TextureHandle prefiltered = renderGraph.CreateTexture(prefilterDesc);
                AddSimplePass(renderGraph, source, prefiltered, Passes.Prefilter, Vector2.zero, "Phosphor Trail Prefilter");

                // ---- Decay previous history into a temp buffer ----
                var decayedDesc = baseDesc;
                decayedDesc.name = "_PhosphorTrail_Decayed";
                TextureHandle decayed = renderGraph.CreateTexture(decayedDesc);
                // Roughly normalize the decay to framerate so trail length
                // doesn't change dramatically between 30/60/144hz etc.
                float frameAdjustedDecay = Mathf.Pow(settings.persistence.value, Time.deltaTime * 60f);
                material.SetFloat("_Decay", frameAdjustedDecay);
                AddSimplePass(renderGraph, history, decayed, Passes.Decay, Vector2.zero, "Phosphor Trail Decay");

                // ---- Combine fresh bright pixels with decayed history, write back into history ----
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Phosphor Trail Combine", out var passData, profilingSampler))
                {
                    passData.material = material;
                    passData.source = prefiltered;
                    passData.combineTex = decayed;
                    passData.passIndex = Passes.Combine;
                    builder.UseTexture(prefiltered, AccessFlags.Read);
                    builder.UseTexture(decayed, AccessFlags.Read);
                    builder.SetRenderAttachment(history, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalTexture("_DecayedTex", data.combineTex);
                        Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                    });
                }

                // ---- Residual: strip out this frame's own bright pixels ----
                // so a static light doesn't add extra brightness on top of
                // itself every frame - only genuine leftover trail remains.
                var residualDesc = baseDesc;
                residualDesc.name = "_PhosphorTrail_Residual";
                TextureHandle residual = renderGraph.CreateTexture(residualDesc);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Phosphor Trail Residual", out var passData, profilingSampler))
                {
                    passData.material = material;
                    passData.source = history;
                    passData.combineTex = prefiltered;
                    passData.passIndex = Passes.Residual;
                    builder.UseTexture(history, AccessFlags.Read);
                    builder.UseTexture(prefiltered, AccessFlags.Read);
                    builder.SetRenderAttachment(residual, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalTexture("_FreshTex", data.combineTex);
                        Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                    });
                }

                // ---- Soften for display only - NOT fed back into history ----
                TextureHandle soft = residual;
                int softIterations = Mathf.Max(0, settings.softness.value);
                for (int i = 0; i < softIterations; i++)
                {
                    var softDesc = baseDesc;
                    softDesc.name = "_PhosphorTrail_Soft" + i;
                    TextureHandle next = renderGraph.CreateTexture(softDesc);
                    AddSimplePass(renderGraph, soft, next, Passes.Soften, new Vector2(1f / width, 1f / height), "Phosphor Trail Soften " + i);
                    soft = next;
                }

                // ---- Composite ----
                var compositeDesc = renderGraph.GetTextureDesc(source);
                compositeDesc.name = "_PhosphorTrail_Composite";
                compositeDesc.clearBuffer = false;
                compositeDesc.msaaSamples = MSAASamples.None;
                TextureHandle compositeOutput = renderGraph.CreateTexture(compositeDesc);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Phosphor Trail Composite", out var passData, profilingSampler))
                {
                    passData.material = material;
                    passData.source = source;
                    passData.combineTex = soft;
                    passData.passIndex = Passes.Composite;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(soft, AccessFlags.Read);
                    builder.SetRenderAttachment(compositeOutput, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalTexture("_TrailTex", data.combineTex);
                        Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                    });
                }

                resourceData.cameraColor = compositeOutput;
            }

            private void AddSimplePass(RenderGraph renderGraph, TextureHandle source, TextureHandle destination, int passIndex, Vector2 texelSize, string passName)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
                {
                    passData.material = material;
                    passData.source = source;
                    passData.passIndex = passIndex;
                    passData.texelSize = texelSize;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                    {
                        data.material.SetVector("_TexelSize", data.texelSize);
                        Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex);
                    });
                }
            }
        }
    }
}
