using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// ReSharper disable once CheckNamespace
namespace Lepsima.Shaders.AutoExposure {
	
public class AutoExposureRendererFeature : ScriptableRendererFeature {
	
	// "After Post-Processing" is not recommended if bloom is enabled, it's manageable otherwise
	public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
	
	[Tooltip("The Blit Shader that applies the average luminance to the screen")]
	public Shader renderShader;
	[Tooltip("The Compute Shader that calculates the average luminance")]
	public ComputeShader computeShader;
	
	private AverageComputePass _computePass;// Computes the average exposure of the screen
	private ExposureRenderPass _renderPass;	// Applies the average exposure to the screen
    private Material _material;				// Auto-generated material using the render shader
	
    private static float _targetExposure = -1f;	// Latest computed average exposure. -1 = uninitialized
    private static float _currentExposure = -1f;	// The actual exposure, always approaching the target exposure. -1 = uninitialized
    private static float _lastTime = 0;			// The timestamp of the last compute pass
    private static int _computeCounter = 0;		// The amount of skipped compute passes, resets on every compute pass
    private static bool _readbackPending = false;	// Prevents queueing a new AsyncGPUReadback while one is still in flight
    private static int _pendingFrameCount = 0;		// Safety counter: how many frames the current readback has been pending
    private const int MaxPendingFrames = 120;		// Safety valve (~2s @ 60fps): force-clear a stuck pending flag so exposure updates can't stall forever

    private static AutoExposure _autoExposure; // Reference to the Volume's auto exposure component
    
	public override void Create() {
		if (!SystemInfo.supportsComputeShaders) {
            Debug.LogWarning("Device does not support compute shaders. Auto Exposure will be disabled.");
			return;
		}

		if (!renderShader || !computeShader) {
			Debug.LogWarning("Render or Compute shader missing. Auto Exposure will be disabled.");
			return;
		}
		
		// Create materials and passes
		_material = CoreUtils.CreateEngineMaterial(renderShader);
		_computePass = new AverageComputePass(computeShader, renderPassEvent);
		_renderPass = new ExposureRenderPass(_material, renderPassEvent);

		// Only reset exposure on first initialization, NOT on subsequent Create() calls
		// (Create is called again on recompile/scene reload, resetting exposure causes a flash)
		if (_targetExposure < 0f) _targetExposure = 0.18f;
		if (_currentExposure < 0f) _currentExposure = 0.18f;
		
		// Always reset stateful counters/flags — these must be clean after a rebuild
		_computeCounter = 0;
		_lastTime = Time.time;
		_readbackPending = false;
		_pendingFrameCount = 0;
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
		// Update the auto exposure component reference
		_autoExposure = VolumeManager.instance.stack.GetComponent<AutoExposure>();
		if (!_autoExposure || !_autoExposure.IsActive()) return;
		
		if (_computePass == null || _renderPass == null) {
			Debug.LogWarning("Graphics or Compute shader missing. The pass will be skipped.");
			return;
		}
		
		// If a readback is pending, count how long it's been stuck. Callbacks can fail to
		// ever fire (device reset, pipeline swap, object destroyed mid-flight, editor recompiles),
		// so if the flag has been stuck for too long we force-clear it instead of blocking
		// exposure updates permanently.
		if (_readbackPending) {
			_pendingFrameCount++;
			if (_pendingFrameCount > MaxPendingFrames) {
				_readbackPending = false;
				_pendingFrameCount = 0;
			}
		}

		// Execute the compute pass once every X frames (3 by default), can be set to 1:1
		// Skip enqueueing it entirely if a previous readback hasn't completed yet, to avoid queueing requests faster than the GPU can service them
		if (!_readbackPending && _computeCounter % Mathf.Max(_autoExposure.framesPerCompute.value, 1.0f) == 0) {
			renderer.EnqueuePass(_computePass);
			_computeCounter = 0;
		}
		_computeCounter++;
		
		// The render pass will take the last exposure compute result
		renderer.EnqueuePass(_renderPass);
		UpdateExposure();
	}
	
	private static void UpdateExposure() {
		if (!_autoExposure) return;
		
		// Delta time, using the built-in one seems to flicker a lot, weird
		float now = Time.time;
		float deltaTime = now - _lastTime;
		_lastTime = now;

		// Exposure difference, skip if zero
		float diff = _targetExposure - _currentExposure;
		if (diff == 0) return;
		
		float decay = diff > 0 ? _autoExposure.increaseSpeed.value : _autoExposure.decreaseSpeed.value;
		_currentExposure = ExpDecay(_currentExposure, _targetExposure, decay, deltaTime);

		if (float.IsNaN(_currentExposure) || _currentExposure < 0f) {
			_currentExposure = 0.18f;
		}
		_currentExposure = Mathf.Max(_currentExposure, 0.001f);
	}

	protected override void Dispose(bool disposing) {
		CoreUtils.Destroy(_material);
	}

	// Set the target exposure, 0 is full Black, 1 is full White and >1 is very bright White
	// Clamp to a safe minimum so the shader divisor (9.6 * _Exposure) never approaches zero
	private static void SetExposure(float avg) {
		if (!_autoExposure) return;
		if (float.IsNaN(avg) || float.IsInfinity(avg) || avg < 0f) return;
		_targetExposure = Mathf.Max(avg, 0.001f);
	}

	// FrameRate independent lerp, Source -> https://www.youtube.com/watch?v=LSNQuFEDOyQ
	public static float ExpDecay(float a, float b, float decay, float deltaTime) => b + (a - b) * Mathf.Exp(-decay * deltaTime);
	
	// Get the smoothed current exposure
	public static float GetExposure() => _currentExposure;
	
	// Compute Shader pass
	private class AverageComputePass : ScriptableRenderPass {
		// Shader parameters
		private static readonly int Dims = Shader.PropertyToID("_Dims");
		private static readonly int NumGroupsX = Shader.PropertyToID("_NumGroupsX");
		private static readonly int Source = Shader.PropertyToID("_Source");
        private static readonly int TempOut = Shader.PropertyToID("_OutBuffer");
        private static readonly int BrightnessLimitId = Shader.PropertyToID("_BrightnessLimit");
        
        // Compute shader reference
        // Must match THREAD_COUNT in AverageExposureCS.compute (32x32 threads per group)
        private const int ThreadGroupSize = 32;
        private readonly ComputeShader _computeShader;

        public AverageComputePass(ComputeShader compute, RenderPassEvent renderPassEvent) {
	        this.renderPassEvent = renderPassEvent;
	        _computeShader = compute;
        }
        
        private class PassData {
            public ComputeShader ComputeShader; // Shader reference
            public TextureHandle Texture;		// Screen texture
	        public Vector2Int Size;				// Size of the screen
	        public Vector2Int GroupCount;		// Number of thread groups dispatched along X and Y
            public BufferHandle Output;			// Output buffer
            public float BrightnessLimit;		// Max luminance a pixel may contribute (mirrors _MaxBrightness in the render shader)
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
	        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
	        
	        // Screen texture
	        TextureHandle texture = resourceData.activeColorTexture;
	        TextureDesc textureDesc = texture.GetDescriptor(renderGraph);
	        
	        // Initialize output buffer, this needs to be done every time in case the screen changes size
	        Vector2Int size = new(textureDesc.width, textureDesc.height);

	        // Number of 32x32 thread groups needed to cover the screen in both dimensions
	        Vector2Int groupCount = new(
		        Mathf.CeilToInt((float)size.x / ThreadGroupSize),
		        Mathf.CeilToInt((float)size.y / ThreadGroupSize)
	        );

	        BufferDesc outBufferDesc = new(groupCount.x * groupCount.y * 2, 4) {
		        target = GraphicsBuffer.Target.Structured,
		        name = "Auto Exposure Output Buffer"
	        };
	        
	        // Create the buffer in the render graph
	        BufferHandle outputHandle = renderGraph.CreateBuffer(outBufferDesc);
            
	        // Build the compute pass
            using IComputeRenderGraphBuilder builder = renderGraph.AddComputePass("Avg Exposure Compute Pass", out PassData passData);
            
            // Set parameters
            passData.ComputeShader = _computeShader;
            passData.Texture = texture;
            passData.Output = outputHandle;
            passData.Size = size;
            passData.GroupCount = groupCount;
            // Read from volume here (record phase, main thread) — ExecutePass runs on render thread
            passData.BrightnessLimit = _autoExposure ? _autoExposure.brightnessLimit.value : 15f;
            
            builder.AllowPassCulling(false); // The output buffer is detected as unused by default, this isn't true
            builder.UseTexture(resourceData.activeColorTexture);
            builder.UseBuffer(passData.Output, AccessFlags.Write);
            builder.SetRenderFunc((PassData data, ComputeGraphContext cgContext) => ExecutePass(data, cgContext));
        }
        
        private static void ExecutePass(PassData data, ComputeGraphContext cgContext) {
	        ComputeCommandBuffer cmd = cgContext.cmd;
	        
	        // Set compute parameters and dispatch
	        cmd.SetComputeIntParams(data.ComputeShader, Dims, data.Size.x, data.Size.y);
	        cmd.SetComputeIntParam(data.ComputeShader, NumGroupsX, data.GroupCount.x);
	        cmd.SetComputeFloatParam(data.ComputeShader, BrightnessLimitId, data.BrightnessLimit);
	        cmd.SetComputeTextureParam(data.ComputeShader,0, Source, data.Texture);
            cmd.SetComputeBufferParam(data.ComputeShader,0, TempOut, data.Output);

            // Dispatch by workgroup count (2D), not by pixel count - each group covers a 32x32 pixel tile
            cmd.DispatchCompute(data.ComputeShader,0, data.GroupCount.x, data.GroupCount.y, 1);

            _readbackPending = true;
            _pendingFrameCount = 0;
            AsyncGPUReadback.Request(data.Output, OnCompleteReadBack);
        }
        
        private static void OnCompleteReadBack(AsyncGPUReadbackRequest request) {
	        _readbackPending = false;
	        _pendingFrameCount = 0;
	        if (request.hasError || !_autoExposure) return;
	        
	        // Use NativeArray directly — no ToArray() copy, no managed heap allocation
	        Unity.Collections.NativeArray<float> groupValues = request.GetData<float>();
	        
	        // Sum all workgroup results. The compute shader already stores log(luminance),
	        // so we sum logs and exponentiate once at the end → geometric mean luminance.
	        // Geometric mean matches human eye perception far better than arithmetic mean
	        // (e.g. avoids a few bright highlights dominating the average).
	        double logLumTotal = 0;
	        uint pixels = 0;
	        int groupCount = groupValues.Length / 2;
	        for (int i = 0; i < groupCount; i++) {
		        logLumTotal += groupValues[i * 2];
		        // shared_pixels only ever accumulates 0/1 integers — cast is exact, no rounding needed
		        pixels += (uint)groupValues[i * 2 + 1];
	        }
	        
	        // exp(average of log(L)) = geometric mean of luminance
	        if (pixels > 0) {
		        float geometricMeanLum = Mathf.Exp((float)(logLumTotal / pixels));
		        SetExposure(geometricMeanLum);
	        }
        }
	}
	
	private class ExposureRenderPass : ScriptableRenderPass { 
		// Shader parameters
		private static readonly int ScreenExposureProp = Shader.PropertyToID("_Exposure");
		private static readonly int ExposureRangeProp = Shader.PropertyToID("_Range");
		private static readonly int WhitePointSqProp = Shader.PropertyToID("_WhitePointSq");
		private static readonly int MaxBrightness = Shader.PropertyToID("_MaxBrightness");
		
		private const string PassNameTempRT = "Exposure Temp Pass";
		private const string PassNameCameraColor = "Exposure Camera Pass";
		private readonly Material _material;
		
		private class PassData {
			public TextureHandle Source;	// Screen texture
			public float Exposure;			// Current exposure
			public Vector2 Range;			// Exposure range
			public float MaxBrightness;		// Maximum pixel brightness
			public float WhitePointSq;		// White point squared (precomputed, passed directly to shader)
			public Material Material;		// Material reference
		}
		
		public ExposureRenderPass(Material material, RenderPassEvent renderPassEvent) {
			this.renderPassEvent = renderPassEvent;
			_material = material;
			
			requiresIntermediateTexture = true;
			profilingSampler = new ProfilingSampler(nameof(ExposureRenderPass));
		}
		
		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
			UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
			UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
			
			// Get current screen texture
			TextureHandle cameraColorTextureHandle = resourceData.activeColorTexture;
			RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
			desc.msaaSamples = 1;
			desc.depthBufferBits = 0;

			// Create the temporal texture pass
			TextureHandle tempTextureHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_TempRT", true);
			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(PassNameTempRT, out PassData passData, profilingSampler)) {
				builder.UseTexture(cameraColorTextureHandle);
				builder.SetRenderAttachment(tempTextureHandle, 0);

				passData.Source = cameraColorTextureHandle;
				passData.Material = _material;
				passData.Exposure = GetExposure();
				passData.MaxBrightness = _autoExposure.brightnessLimit.value;
				passData.Range = _autoExposure.exposureRange.value;
				// Square on CPU once per frame here, not inside ExecutePass which runs per-pass
				float wp = _autoExposure.whitePoint.value;
				passData.WhitePointSq = wp * wp;
				
				builder.SetRenderFunc((PassData data, RasterGraphContext graphContext) => ExecutePass(data, graphContext));
			}
			
			// Apply back the temporal texture to the screen
			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(PassNameCameraColor, out PassData passData, profilingSampler)) {
				builder.UseTexture(tempTextureHandle);
				builder.SetRenderAttachment(cameraColorTextureHandle, 0);
				
				passData.Source = tempTextureHandle;
				passData.Material = null;
				
				builder.SetRenderFunc((PassData data, RasterGraphContext graphContext) => ExecutePass(data, graphContext));
			}
		}

		private static void ExecutePass(PassData passData, RasterGraphContext graphContext) {
			RasterCommandBuffer cmd = graphContext.cmd;

			// Temporal texture pass
			if (passData.Material) {
				passData.Material.SetFloat(ScreenExposureProp, passData.Exposure);
				passData.Material.SetVector(ExposureRangeProp, passData.Range);
				passData.Material.SetFloat(WhitePointSqProp, passData.WhitePointSq); // already squared at record time
				passData.Material.SetFloat(MaxBrightness, passData.MaxBrightness);
				Blitter.BlitTexture(cmd, passData.Source, new Vector4(1, 1, 0, 0), passData.Material, 0);
			}
			// Back to screen pass
			else {
				Blitter.BlitTexture(cmd, passData.Source, new Vector4(1, 1, 0, 0), 0, false);
			}
		}
	}
}
}