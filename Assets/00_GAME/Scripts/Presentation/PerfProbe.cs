// PerfProbe - the frame ledger behind Docs/PERFORMANCE.md.
// Layer: Presentation.
// Responsibility: read Unity's own frame counters through ProfilerRecorder and log one line per
//   second: mean and worst frame time, main and render thread time, GC bytes and allocation
//   count per frame, batches, SetPass calls, draw calls, shadow casters, triangles. The same
//   component measures the editor and a development build on the phone, so the two columns of
//   every table in the ledger come from one instrument.
// NOT its responsibility: changing anything. It never sets targetFrameRate or quality; it only
//   reads. It also does not run in a release build: Debug.isDebugBuild gates it in Awake, and
//   the counters it reads do not exist there anyway.
//
// The log line itself allocates (number formatting), so the frame after a log is left out of the
// GC sums: what the line reports is the game's allocation, not the probe's.

using System.Text;
using TMPro;
using Unity.Profiling;
using UnityEngine;

namespace Blast.Presentation
{
    /// <summary>Logs frame time, allocation and render counters once a second, in the editor and development builds only.</summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        #region Fields

        /// <summary>Seconds between log lines.</summary>
        const float Window = 1f;

        /// <summary>Nanoseconds in a millisecond, for the thread-time counters.</summary>
        const float NsPerMs = 1_000_000f;

        /// <summary>Thousands of triangles read better than the raw count.</summary>
        const float TrianglesPerK = 1000f;

        /// <summary>Managed bytes allocated in the frame.</summary>
        ProfilerRecorder _gcBytes;

        /// <summary>Number of managed allocations in the frame.</summary>
        ProfilerRecorder _gcCount;

        /// <summary>Main thread time for the frame, in nanoseconds.</summary>
        ProfilerRecorder _mainThread;

        /// <summary>Render thread time for the frame, in nanoseconds.</summary>
        ProfilerRecorder _renderThread;

        /// <summary>Batches submitted in the frame (after SRP batching).</summary>
        ProfilerRecorder _batches;

        /// <summary>Shader/material switches in the frame.</summary>
        ProfilerRecorder _setPass;

        /// <summary>Draw calls in the frame.</summary>
        ProfilerRecorder _draws;

        /// <summary>Renderers drawn into the shadow map.</summary>
        ProfilerRecorder _shadowCasters;

        /// <summary>Triangles drawn in the frame.</summary>
        ProfilerRecorder _triangles;

        /// <summary>Optional on-screen readout, written once a second with the same numbers the log gets. Null means log only.</summary>
        [Tooltip("Optional TMP text for an on-screen fps / ms / GC readout. Leave empty to log only.")]
        [SerializeField] TMP_Text _hud;

        /// <summary>The line under construction, reused so the probe allocates only for number formatting.</summary>
        readonly StringBuilder _line = new StringBuilder(256);

        /// <summary>The short form for the on-screen readout, reused the same way.</summary>
        readonly StringBuilder _hudLine = new StringBuilder(64);

        /// <summary>Seconds accumulated in the current window.</summary>
        float _elapsed;

        /// <summary>Frames counted in the current window.</summary>
        int _frames;

        /// <summary>Longest frame in the current window, in seconds.</summary>
        float _worst;

        /// <summary>Managed bytes summed over the window's counted frames.</summary>
        long _gcBytesTotal;

        /// <summary>Managed allocations summed over the window's counted frames.</summary>
        long _gcCountTotal;

        /// <summary>Frames whose GC counters were summed (the frame after a log is skipped).</summary>
        int _gcFrames;

        /// <summary>True right after a log line, so its own formatting garbage is not counted.</summary>
        bool _skipGcSample;

        #endregion

        #region Private Methods

        /// <summary>Switches the probe off outside the editor and development builds.</summary>
        void Awake()
        {
            if (!Debug.isDebugBuild) enabled = false;
        }

        /// <summary>Starts every recorder.</summary>
        void OnEnable()
        {
            _gcBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
            _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            _renderThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _shadowCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        /// <summary>Disposes every recorder.</summary>
        void OnDisable()
        {
            _gcBytes.Dispose();
            _gcCount.Dispose();
            _mainThread.Dispose();
            _renderThread.Dispose();
            _batches.Dispose();
            _setPass.Dispose();
            _draws.Dispose();
            _shadowCasters.Dispose();
            _triangles.Dispose();
        }

        /// <summary>Accumulates the frame and logs when the window is full.</summary>
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            _frames++;
            if (dt > _worst) _worst = dt;

            if (_skipGcSample)
            {
                _skipGcSample = false;
            }
            else
            {
                _gcBytesTotal += _gcBytes.LastValue;
                _gcCountTotal += _gcCount.LastValue;
                _gcFrames++;
            }

            if (_elapsed < Window) return;

            Log();
            _elapsed = 0f;
            _frames = 0;
            _worst = 0f;
            _gcBytesTotal = 0;
            _gcCountTotal = 0;
            _gcFrames = 0;
            _skipGcSample = true;
        }

        /// <summary>Formats and prints the window's numbers as one line.</summary>
        void Log()
        {
            _line.Clear();
            _line.Append("[perf] fps ").Append((_frames / _elapsed).ToString("F1"));
            _line.Append(" | frame ").Append((_elapsed / _frames * 1000f).ToString("F2")).Append(" ms worst ").Append((_worst * 1000f).ToString("F1"));
            _line.Append(" | main ").Append(Ms(_mainThread)).Append(" render ").Append(Ms(_renderThread));
            _line.Append(" | gc ").Append(_gcFrames > 0 ? _gcBytesTotal / _gcFrames : 0).Append(" B/f x").Append(_gcFrames > 0 ? _gcCountTotal / _gcFrames : 0);
            _line.Append(" | batches ").Append(Count(_batches)).Append(" setpass ").Append(Count(_setPass)).Append(" draws ").Append(Count(_draws));
            _line.Append(" shadow ").Append(Count(_shadowCasters)).Append(" tris ").Append((Count(_triangles) / TrianglesPerK).ToString("F1")).Append('k');
            Debug.Log(_line.ToString());

            if (_hud == null) return;
            _hudLine.Clear();
            _hudLine.Append((_frames / _elapsed).ToString("F0")).Append(" fps  ")
                .Append((_elapsed / _frames * 1000f).ToString("F1")).Append(" ms  gc ")
                .Append(_gcFrames > 0 ? _gcBytesTotal / _gcFrames : 0).Append(" B");
            // SetText(StringBuilder): no string is built for the mesh update, and it lands on the frame the GC sum skips.
            _hud.SetText(_hudLine);
        }

        /// <summary>The recorder's last frame value, or -1 where the counter does not exist on this platform.</summary>
        static long Count(ProfilerRecorder recorder) => recorder.Valid ? recorder.LastValue : -1;

        /// <summary>A thread-time recorder's last frame as milliseconds, or "-" where it does not exist.</summary>
        static string Ms(ProfilerRecorder recorder) => recorder.Valid ? (recorder.LastValue / NsPerMs).ToString("F2") : "-";

        #endregion
    }
}
