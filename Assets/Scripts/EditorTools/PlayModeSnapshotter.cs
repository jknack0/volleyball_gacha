using UnityEditor;
using UnityEngine;

namespace VG.EditorTools
{
    /// <summary>
    /// While the editor is in Play mode, writes the Game view to /tmp/vg_frame.png every
    /// couple of seconds. Lets tooling (or a remote pair) see what the user sees without
    /// manual screenshots. Dev-only convenience; costs nothing outside Play mode.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeSnapshotter
    {
        private const string Path = "/tmp/vg_frame.png";
        private const float IntervalSeconds = 2f; // [tunable]
        private static double _next;

        static PlayModeSnapshotter()
        {
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + IntervalSeconds;
            ScreenCapture.CaptureScreenshot(Path);
        }
    }
}
