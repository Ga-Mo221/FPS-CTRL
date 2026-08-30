// ASP Global Wind - ANGRY MESH - contact@angrymesh.com
// PERFORMANCE OPTIMIZED:
//   - Shader.PropertyToID cached as static readonly (removes string hash lookup every frame)
//   - Dirty checking: skips SetGlobal calls when values haven't changed
//   - WindToggle only updated when EnableGlobalWind actually changes

using UnityEngine;

namespace ANGRYMESH.StylizedPack
{
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshRenderer))]
    public class ASPGlobalWind : MonoBehaviour
    {
        public bool EnableGlobalWind = true;
        float WindToggle = 0.0f;

        [Header("Wind Tree Settings")]
        [Range(0.0f, 10.0f)] public float WindTreeAmplitude = 1.0f;
        [Range(0.0f, 10.0f)] public float WindTreeSpeed = 1.0f;
        [Range(0.0f, 10.0f)] public float WindTreeFlexibility = 1.0f;

        [Range(0.0f, 10.0f)] public float WindTreeLeafAmplitude = 1.0f;
        [Range(0.0f, 10.0f)] public float WindTreeLeafSpeed = 1.0f;
        [Range(0.0f, 10.0f)] public float WindTreeLeafTurbulence = 1.0f;
        [Range(0.0f, 10.0f)] public float WindTreeLeafOffset = 1.0f;

        [Header("Wind Grass Settings")]
        [Range(0.0f, 10.0f)] public float WindGrassAmplitude = 1.0f;
        [Range(0.0f, 10.0f)] public float WindGrassSpeed = 1.0f;
        [Range(0.0f, 10.0f)] public float WindGrassTurbulence = 1.0f;
        [Range(0.0f, 10.0f)] public float WindGrassFlexibility = 1.0f;
        [Range(0.0f, 10.0f)] public float WindGrassWavesAmplitude = 1.0f;
        [Range(0.0f, 10.0f)] public float WindGrassWavesSpeed = 1.0f;
        [Range(0.0f, 10.0f)] public float WindGrassWavesScale = 1.0f;

        public Texture2D WindGrassWavesNoiseTexture = null;

        private MeshRenderer meshRenderer;

        // OPT: Cache Property IDs once (static readonly = computed once at class load, shared across all instances)
        // Replaces string hash lookup in Shader.SetGlobal*() on every Update() call
        private static readonly int ID_WindDirection           = Shader.PropertyToID("ASPW_WindDirection");
        private static readonly int ID_WindToggle              = Shader.PropertyToID("ASPW_WindToggle");

        private static readonly int ID_WindTreeAmplitude       = Shader.PropertyToID("ASPW_WindTreeAmplitude");
        private static readonly int ID_WindTreeSpeed           = Shader.PropertyToID("ASPW_WindTreeSpeed");
        private static readonly int ID_WindTreeFlexibility     = Shader.PropertyToID("ASPW_WindTreeFlexibility");

        private static readonly int ID_WindTreeLeafAmplitude   = Shader.PropertyToID("ASPW_WindTreeLeafAmplitude");
        private static readonly int ID_WindTreeLeafSpeed       = Shader.PropertyToID("ASPW_WindTreeLeafSpeed");
        private static readonly int ID_WindTreeLeafTurbulence  = Shader.PropertyToID("ASPW_WindTreeLeafTurbulence");
        private static readonly int ID_WindTreeLeafOffset      = Shader.PropertyToID("ASPW_WindTreeLeafOffset");

        private static readonly int ID_WindGrassAmplitude      = Shader.PropertyToID("ASPW_WindGrassAmplitude");
        private static readonly int ID_WindGrassSpeed          = Shader.PropertyToID("ASPW_WindGrassSpeed");
        private static readonly int ID_WindGrassTurbulence     = Shader.PropertyToID("ASPW_WindGrassTurbulence");
        private static readonly int ID_WindGrassFlexibility    = Shader.PropertyToID("ASPW_WindGrassFlexibility");
        private static readonly int ID_WindGrassWavesAmplitude = Shader.PropertyToID("ASPW_WindGrassWavesAmplitude");
        private static readonly int ID_WindGrassWavesSpeed     = Shader.PropertyToID("ASPW_WindGrassWavesSpeed");
        private static readonly int ID_WindGrassWavesScale     = Shader.PropertyToID("ASPW_WindGrassWavesScale");
        private static readonly int ID_WindGrassWavesNoise     = Shader.PropertyToID("ASPW_WindGrassWavesNoiseTexture");

        // OPT: Previous-frame values for dirty checking
        // SetGlobal*() has a small but non-zero cost even when the value didn't change
        private bool      _prevEnableWind;
        private Vector3   _prevWindDirection;
        private float     _prevWindTreeAmplitude;
        private float     _prevWindTreeSpeed;
        private float     _prevWindTreeFlexibility;
        private float     _prevWindTreeLeafAmplitude;
        private float     _prevWindTreeLeafSpeed;
        private float     _prevWindTreeLeafTurbulence;
        private float     _prevWindTreeLeafOffset;
        private float     _prevWindGrassAmplitude;
        private float     _prevWindGrassSpeed;
        private float     _prevWindGrassTurbulence;
        private float     _prevWindGrassFlexibility;
        private float     _prevWindGrassWavesAmplitude;
        private float     _prevWindGrassWavesSpeed;
        private float     _prevWindGrassWavesScale;
        private Texture2D _prevWindGrassWavesNoiseTexture;

        // OPT: Cache _CullMode property ID (used once in Start, but follows the same pattern)
        private static readonly int ID_CullMode = Shader.PropertyToID("_CullMode");

        // Disable Arrow in play mode
        void Start()
        {
            meshRenderer = gameObject.GetComponent<MeshRenderer>();
            meshRenderer.enabled = !Application.isPlaying;

            // OPT MOBILE: Set Cull Back on mobile/Switch to eliminate back-face fill cost.
            // PC/Console: keep Cull Off (double-sided) so grass is visible from all angles.
            // This writes to the material property that drives the Cull [_CullMode] directive.
            //   0 = CullMode.Off  (double-sided, PC default)
            //   2 = CullMode.Back (single-sided, mobile)
#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS || UNITY_SWITCH
            const float mobileCull = 2f; // CullMode.Back
#else
            const float mobileCull = 0f; // CullMode.Off
#endif
            // Apply to all shared materials on this renderer (grass material)
            foreach (var mat in meshRenderer.sharedMaterials)
            {
                if (mat != null) mat.SetFloat(ID_CullMode, mobileCull);
            }

            // Force full sync on first frame
            _prevEnableWind = !EnableGlobalWind;
        }

        void Update()
        {
            // OPT: WindToggle - only call SetGlobal when state actually changes
            if (EnableGlobalWind != _prevEnableWind)
            {
                _prevEnableWind = EnableGlobalWind;
                WindToggle = EnableGlobalWind ? 1.0f : 0.0f;
                Shader.SetGlobalFloat(ID_WindToggle, WindToggle);
            }

            // OPT: Wind Direction - only update when transform has actually rotated
            Vector3 currentDir = gameObject.transform.forward;
            if (currentDir != _prevWindDirection)
            {
                _prevWindDirection = currentDir;
                Shader.SetGlobalVector(ID_WindDirection, currentDir);
            }

            // --- Tree settings ---
            if (WindTreeAmplitude != _prevWindTreeAmplitude)
            {
                _prevWindTreeAmplitude = WindTreeAmplitude;
                Shader.SetGlobalFloat(ID_WindTreeAmplitude, WindTreeAmplitude);
            }
            if (WindTreeSpeed != _prevWindTreeSpeed)
            {
                _prevWindTreeSpeed = WindTreeSpeed;
                Shader.SetGlobalFloat(ID_WindTreeSpeed, WindTreeSpeed);
            }
            if (WindTreeFlexibility != _prevWindTreeFlexibility)
            {
                _prevWindTreeFlexibility = WindTreeFlexibility;
                Shader.SetGlobalFloat(ID_WindTreeFlexibility, WindTreeFlexibility);
            }

            // --- Tree Leaf settings ---
            if (WindTreeLeafAmplitude != _prevWindTreeLeafAmplitude)
            {
                _prevWindTreeLeafAmplitude = WindTreeLeafAmplitude;
                Shader.SetGlobalFloat(ID_WindTreeLeafAmplitude, WindTreeLeafAmplitude);
            }
            if (WindTreeLeafSpeed != _prevWindTreeLeafSpeed)
            {
                _prevWindTreeLeafSpeed = WindTreeLeafSpeed;
                Shader.SetGlobalFloat(ID_WindTreeLeafSpeed, WindTreeLeafSpeed);
            }
            if (WindTreeLeafTurbulence != _prevWindTreeLeafTurbulence)
            {
                _prevWindTreeLeafTurbulence = WindTreeLeafTurbulence;
                Shader.SetGlobalFloat(ID_WindTreeLeafTurbulence, WindTreeLeafTurbulence);
            }
            if (WindTreeLeafOffset != _prevWindTreeLeafOffset)
            {
                _prevWindTreeLeafOffset = WindTreeLeafOffset;
                Shader.SetGlobalFloat(ID_WindTreeLeafOffset, WindTreeLeafOffset);
            }

            // --- Grass settings ---
            if (WindGrassAmplitude != _prevWindGrassAmplitude)
            {
                _prevWindGrassAmplitude = WindGrassAmplitude;
                Shader.SetGlobalFloat(ID_WindGrassAmplitude, WindGrassAmplitude);
            }
            if (WindGrassSpeed != _prevWindGrassSpeed)
            {
                _prevWindGrassSpeed = WindGrassSpeed;
                Shader.SetGlobalFloat(ID_WindGrassSpeed, WindGrassSpeed);
            }
            if (WindGrassTurbulence != _prevWindGrassTurbulence)
            {
                _prevWindGrassTurbulence = WindGrassTurbulence;
                Shader.SetGlobalFloat(ID_WindGrassTurbulence, WindGrassTurbulence);
            }
            if (WindGrassFlexibility != _prevWindGrassFlexibility)
            {
                _prevWindGrassFlexibility = WindGrassFlexibility;
                Shader.SetGlobalFloat(ID_WindGrassFlexibility, WindGrassFlexibility);
            }
            if (WindGrassWavesAmplitude != _prevWindGrassWavesAmplitude)
            {
                _prevWindGrassWavesAmplitude = WindGrassWavesAmplitude;
                Shader.SetGlobalFloat(ID_WindGrassWavesAmplitude, WindGrassWavesAmplitude);
            }
            if (WindGrassWavesSpeed != _prevWindGrassWavesSpeed)
            {
                _prevWindGrassWavesSpeed = WindGrassWavesSpeed;
                Shader.SetGlobalFloat(ID_WindGrassWavesSpeed, WindGrassWavesSpeed);
            }
            if (WindGrassWavesScale != _prevWindGrassWavesScale)
            {
                _prevWindGrassWavesScale = WindGrassWavesScale;
                Shader.SetGlobalFloat(ID_WindGrassWavesScale, WindGrassWavesScale);
            }

            // OPT: Texture reference check (reference equality, no cost)
            if (WindGrassWavesNoiseTexture != _prevWindGrassWavesNoiseTexture)
            {
                _prevWindGrassWavesNoiseTexture = WindGrassWavesNoiseTexture;
                Shader.SetGlobalTexture(ID_WindGrassWavesNoise, WindGrassWavesNoiseTexture);
            }
        }

#if UNITY_EDITOR
        // OPT: In Editor, Inspector changes bypass the dirty check (prev values don't update via slider drag)
        // OnValidate ensures a full sync whenever any value is changed in the Inspector
        void OnValidate()
        {
            // Reset all prev values to force a full SetGlobal pass on the next Update()
            _prevEnableWind              = !EnableGlobalWind;
            _prevWindDirection           = Vector3.positiveInfinity;
            _prevWindTreeAmplitude       = float.NaN;
            _prevWindTreeSpeed           = float.NaN;
            _prevWindTreeFlexibility     = float.NaN;
            _prevWindTreeLeafAmplitude   = float.NaN;
            _prevWindTreeLeafSpeed       = float.NaN;
            _prevWindTreeLeafTurbulence  = float.NaN;
            _prevWindTreeLeafOffset      = float.NaN;
            _prevWindGrassAmplitude      = float.NaN;
            _prevWindGrassSpeed          = float.NaN;
            _prevWindGrassTurbulence     = float.NaN;
            _prevWindGrassFlexibility    = float.NaN;
            _prevWindGrassWavesAmplitude = float.NaN;
            _prevWindGrassWavesSpeed     = float.NaN;
            _prevWindGrassWavesScale     = float.NaN;
            _prevWindGrassWavesNoiseTexture = null;
        }
#endif
    }
}