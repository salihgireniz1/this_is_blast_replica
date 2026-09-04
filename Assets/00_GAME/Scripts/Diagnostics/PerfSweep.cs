// PerfSweep - one build, every render variant, for Docs/PERFORMANCE.md.
// Layer: Diagnostics (development builds and the Editor only; references no game layer).
// Responsibility: walk a fixed list of render settings on a timer and log which one is live,
//   so PerfProbe's lines can be read per variant. Each variant is applied on top of the
//   scene's own values and undone before the next, so every number is one change against
//   the same baseline. The list is rewritten per experiment; the ledger records which list
//   produced which table.
// NOT its responsibility: measuring (PerfProbe) or shipping any of these settings. It runs
//   only where Debug.isDebugBuild is true and only when enabled in the inspector; everything
//   it touches is restored when the sweep ends or the component is disabled.
//
// Why a runtime sweep instead of one build per variant: an incremental Android build is
// three minutes and a variant is one line; six builds would have measured the same scene
// six times with six chances of the phone thermally throttling in between.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Blast.Diagnostics
{
    /// <summary>Cycles through render-setting variants on a timer and logs each one, development builds only.</summary>
    public sealed class PerfSweep : MonoBehaviour
    {
        #region Fields

        /// <summary>Seconds each variant is left running; the probe logs once a second, so six lines each.</summary>
        const float VariantSeconds = 6f;

        /// <summary>The variants, in order. Index 0 is the untouched baseline.</summary>
        static readonly string[] Names =
        {
            "baseline (hard, no post, no hdr)",
            "post + hdr",
            "soft low",
            "soft low + post + hdr",
            "soft high + post + hdr",
        };

        /// <summary>Whether the sweep runs at all. Off by default; on for a measurement build.</summary>
        [Tooltip("Run the render-setting sweep on start. Leave off except for a measurement build.")]
        [SerializeField] bool _run;

        /// <summary>The camera whose post processing is toggled.</summary>
        [Tooltip("The camera whose post processing the sweep switches off and on.")]
        [SerializeField] Camera _camera;

        /// <summary>The pipeline asset in use, read once.</summary>
        UniversalRenderPipelineAsset _pipeline;

        /// <summary>The camera's URP data, read once.</summary>
        UniversalAdditionalCameraData _cameraData;

        /// <summary>The scene's main light, whose shadow type is toggled.</summary>
        Light _sun;

        /// <summary>The main light's URP data, whose soft shadow quality is toggled.</summary>
        UniversalAdditionalLightData _sunData;

        /// <summary>The asset's own HDR flag, restored between variants.</summary>
        bool _hdr;

        /// <summary>The light's own shadow type, restored between variants.</summary>
        LightShadows _lightShadows;

        /// <summary>The light's own soft shadow quality, restored between variants.</summary>
        SoftShadowQuality _softQuality;

        /// <summary>The camera's own post-processing flag, restored between variants.</summary>
        bool _post;

        /// <summary>
        /// True once the baseline has been read. Unity calls OnDisable even when Awake itself
        /// disabled the component, and Restore before this point would write zeros (a render
        /// scale of 0 shipped once, measured as a blurry phone: Docs/PERFORMANCE.md 2f).
        /// </summary>
        bool _armed;

        /// <summary>Seconds the current variant has been live.</summary>
        float _elapsed;

        /// <summary>Index into Names of the variant currently applied; Names.Length once the sweep is over.</summary>
        int _variant;

        #endregion

        #region Private Methods

        /// <summary>Reads the baseline values and switches the sweep off where it must not run.</summary>
        void Awake()
        {
            _pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            _cameraData = _camera != null ? _camera.GetUniversalAdditionalCameraData() : null;
            _sun = RenderSettings.sun != null ? RenderSettings.sun : FindFirstObjectByType<Light>();
            if (!_run || !Debug.isDebugBuild || _pipeline == null || _cameraData == null || _sun == null)
            {
                enabled = false;
                return;
            }

            _sunData = _sun.GetUniversalAdditionalLightData();
            _hdr = _pipeline.supportsHDR;
            _lightShadows = _sun.shadows;
            _softQuality = _sunData.softShadowQuality;
            _post = _cameraData.renderPostProcessing;
            _armed = true;
        }

        /// <summary>Applies the baseline once the level has spawned.</summary>
        void Start() => Apply(0);

        /// <summary>Advances to the next variant when the current one has had its time.</summary>
        void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < VariantSeconds) return;

            _elapsed = 0f;
            _variant++;
            if (_variant < Names.Length)
            {
                Apply(_variant);
                return;
            }

            Restore();
            Debug.Log("[sweep] done, baseline restored");
            enabled = false;
        }

        /// <summary>Puts everything back exactly as it was read in Awake.</summary>
        void OnDisable() => Restore();

        /// <summary>Restores the baseline, then applies one variant on top of it and logs its name.</summary>
        void Apply(int variant)
        {
            Restore();
            switch (variant)
            {
                case 1:
                    SetPost(true);
                    break;
                case 2:
                    SetSoft(SoftShadowQuality.Low);
                    break;
                case 3:
                    SetSoft(SoftShadowQuality.Low);
                    SetPost(true);
                    break;
                case 4:
                    SetSoft(SoftShadowQuality.High);
                    SetPost(true);
                    break;
            }

            Debug.Log("[sweep] " + Names[variant]);
        }

        /// <summary>Writes the baseline values back.</summary>
        void Restore()
        {
            if (!_armed) return;
            _pipeline.supportsHDR = _hdr;
            _sun.shadows = _lightShadows;
            _sunData.softShadowQuality = _softQuality;
            _cameraData.renderPostProcessing = _post;
        }

        /// <summary>Switches post processing and the HDR target it needs on or off together.</summary>
        void SetPost(bool on)
        {
            _cameraData.renderPostProcessing = on;
            _pipeline.supportsHDR = on;
        }

        /// <summary>Makes the light's shadows soft at the given quality.</summary>
        void SetSoft(SoftShadowQuality quality)
        {
            _sun.shadows = LightShadows.Soft;
            _sunData.softShadowQuality = quality;
        }

        #endregion
    }
}
