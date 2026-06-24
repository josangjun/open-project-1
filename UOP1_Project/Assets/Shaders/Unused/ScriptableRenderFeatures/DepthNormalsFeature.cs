// This code is an adaptation of the open-source work by Alexander Ameye
// From a tutorial originally posted here:
// https://alexanderameye.github.io/outlineshader
// Code also available on his Gist account
// https://gist.github.com/AlexanderAmeye

#pragma warning disable CS0618, CS0672

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DepthNormalsFeature : ScriptableRendererFeature
{
    class DepthNormalsPass : ScriptableRenderPass
    {
        const int kDepthBufferBits = 32;
        private RTHandle m_DepthAttachmentHandle { get; set; }
        internal RenderTextureDescriptor descriptor { get; private set; }

        private Material depthNormalsMaterial = null;
        private FilteringSettings m_FilteringSettings;
        string m_ProfilerTag = "DepthNormals Prepass";
        ShaderTagId m_ShaderTagId = new ShaderTagId("DepthOnly");

        public DepthNormalsPass(RenderQueueRange renderQueueRange, int layerMask, Material material)
        {
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            depthNormalsMaterial = material;
        }

        public void Setup(RenderTextureDescriptor baseDescriptor, RTHandle depthAttachmentHandle)
        {
            this.m_DepthAttachmentHandle = depthAttachmentHandle;
            baseDescriptor.colorFormat = RenderTextureFormat.ARGB32;
            baseDescriptor.depthBufferBits = kDepthBufferBits;
            descriptor = baseDescriptor;
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in an performance manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(m_DepthAttachmentHandle);
            ConfigureClear(ClearFlag.All, Color.black);
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
			
            using (new ProfilingScope(cmd, new ProfilingSampler(m_ProfilerTag)))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(m_ShaderTagId, ref renderingData, sortFlags);
                drawSettings.perObjectData = PerObjectData.None;

                drawSettings.overrideMaterial = depthNormalsMaterial;

                context.DrawRenderers(renderingData.cullResults, ref drawSettings,
                    ref m_FilteringSettings);

                cmd.SetGlobalTexture("_CameraDepthNormalsTexture", m_DepthAttachmentHandle);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// Cleanup any allocated resources that were created during the execution of this render pass.
        public override void FrameCleanup(CommandBuffer cmd)
        {
        }
    }

    DepthNormalsPass depthNormalsPass;
    RTHandle depthNormalsTexture;
    Material depthNormalsMaterial;

    public override void Create()
    {
        depthNormalsMaterial = CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture");
        depthNormalsPass = new DepthNormalsPass(RenderQueueRange.opaque, -1, depthNormalsMaterial);
        depthNormalsPass.renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
        desc.colorFormat = RenderTextureFormat.ARGB32;
        desc.depthBufferBits = 32;

        RenderingUtils.ReAllocateHandleIfNeeded(ref depthNormalsTexture, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_CameraDepthNormalsTexture");

        depthNormalsPass.Setup(desc, depthNormalsTexture);
        renderer.EnqueuePass(depthNormalsPass);
    }

    protected override void Dispose(bool disposing)
    {
        depthNormalsTexture?.Release();
        if (depthNormalsMaterial != null)
        {
            CoreUtils.Destroy(depthNormalsMaterial);
        }
    }
}
