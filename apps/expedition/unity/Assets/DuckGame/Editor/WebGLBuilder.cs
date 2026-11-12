using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DuckGame.EditorTools
{
    /// <summary>
    /// Headless WebGL build entry point, invoked by tools/build-webgl.ps1:
    ///
    ///   Unity.exe -quit -batchmode -nographics ^
    ///       -projectPath apps/training-range/unity ^
    ///       -buildTarget WebGL ^
    ///       -executeMethod DuckGame.EditorTools.WebGLBuilder.Build ^
    ///       -logFile build.log
    ///
    /// It exists because a bare Unity build needs a scene list and WebGL settings that
    /// a fresh clone does not have set up. Doing this in C# leaves the Play-mode setup
    /// a human uses untouched, and keeps the publish output a plain static folder that
    /// any host can serve.
    ///
    /// The scene is chosen by what is actually in the project: an app that has
    /// ExpeditionHub.unity builds that, an app that only has Playground.unity builds
    /// that. No per-app parameter is needed, so this file is identical in both apps.
    /// </summary>
    public static class WebGLBuilder
    {
        /// <summary>Entry scenes in priority order. First one found wins.</summary>
        static readonly (string path, string label)[] EntryScenes =
        {
            ("Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity", "Grove Camp"),
            ("Assets/DuckGame/Generated/v03/Playground.unity",         "Training Range"),
        };

        [MenuItem("Duck Game/Build WebGL (publish)")]
        public static void Build()
        {
            var target = ResolveScene();
            if (target == null)
            {
                Fail("No entry scene found. Expected one of:\n" +
                     string.Join("\n", EntryScenes.Select(e => "  " + e.path)));
                return;
            }

            // Refuse to ship a build whose scripts never compiled: batchmode exits 0
            // even when compilation fails, which reads like a runtime bug later.
            if (EditorUtility.scriptCompilationFailed)
            {
                Fail("Scripts have compile errors; fix them before building.");
                return;
            }

            var outputDir = ResolveOutputDir();
            Directory.CreateDirectory(outputDir);
            ApplyWebGLSettings();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { target.path },
                locationPathName = outputDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            };

            Debug.Log($"[WebGLBuilder] scene={target.path} label={target.label} out={outputDir}");
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"Build failed: {summary.result} " +
                     $"errors={summary.totalErrors} warnings={summary.totalWarnings}");
                return;
            }

            WriteManifest(outputDir, target);
            Debug.Log($"[WebGLBuilder] OK size={summary.totalSize / (1024 * 1024)} MB " +
                      $"time={summary.totalTime} out={outputDir}");
        }

        /// <summary>
        /// <c>&lt;repo&gt;/docs/build-webgl/&lt;app&gt;</c> when the project sits at
        /// <c>&lt;repo&gt;/apps/&lt;app&gt;/unity</c>, otherwise a folder beside the
        /// Unity project so the build still lands somewhere predictable.
        /// </summary>
        static string ResolveOutputDir()
        {
            // Application.dataPath is "<unity>/Assets"; "<unity>" is its parent.
            var unityDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var appDir = Path.GetFullPath(Path.Combine(unityDir, ".."));      // apps/<app>
            var appName = Path.GetFileName(appDir);
            var appsRoot = Path.GetFullPath(Path.Combine(appDir, ".."));      // apps
            var repoRoot = Path.GetFullPath(Path.Combine(appsRoot, ".."));    // repo

            var looksLikeRepo =
                Directory.Exists(Path.Combine(repoRoot, "foundation")) &&
                Directory.Exists(Path.Combine(repoRoot, ".kiro"));

            var root = looksLikeRepo ? Path.Combine(repoRoot, "docs", "build-webgl")
                                     : Path.Combine(unityDir, "build-webgl");
            return Path.Combine(root, appName);
        }

        static (string path, string label)? ResolveScene()
        {
            var unityDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            foreach (var candidate in EntryScenes)
            {
                if (File.Exists(Path.Combine(unityDir, candidate.path))) return candidate;
            }

            // Fall back to whatever the project's build settings already list.
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.enabled && File.Exists(Path.Combine(unityDir, s.path)))
                {
                    return (s.path, Path.GetFileNameWithoutExtension(s.path));
                }
            }
            return null;
        }

        /// <summary>
        /// WebGL needs these; a headless run would otherwise inherit whatever the last
        /// interactive session left behind.
        /// </summary>
        static void ApplyWebGLSettings()
        {
            PlayerSettings.companyName = string.IsNullOrEmpty(PlayerSettings.companyName)
                ? "gushu8861" : PlayerSettings.companyName;
            PlayerSettings.productName = "Duck";
            PlayerSettings.runInBackground = true;

            // Compression off = host-agnostic. Brotli/Gzip need response headers that a
            // plain static host does not send, and these builds are small anyway.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Low);
        }

        /// <summary>
        /// The output is a plain static folder, so leave a manifest naming the build
        /// and its controls beside index.html.
        /// </summary>
        static void WriteManifest(string dir, (string path, string label)? scene)
        {
            const string controls =
                "WASD move | Shift sprint | Space jump | Ctrl slide\n" +
                "Mouse aim | Right mouse hold aim down sights | Left mouse fire\n" +
                "F interact | Tab toggle HUD | Esc release mouse";

            var text =
                $"# {scene?.label ?? "Duck"} - WebGL build\n\n" +
                $"Built from `{scene?.path}`\n" +
                $"Unity {Application.unityVersion}\n\n" +
                "## Controls\n\n```\n" + controls + "\n```\n\n" +
                "Serve this folder over HTTP; opening index.html from disk will not work.\n\n" +
                "```\nnpx --yes serve .\n```\n";

            File.WriteAllText(Path.Combine(dir, "BUILD.md"), text);

            var html = Path.Combine(dir, "index.html");
            if (File.Exists(html))
            {
                var content = File.ReadAllText(html);
                content = content.Replace("<title>Unity WebGL Player | ",
                                          $"<title>{scene?.label ?? "Duck"} | ");
                File.WriteAllText(html, content);
            }
        }

        static void Fail(string message)
        {
            Debug.LogError("[WebGLBuilder] " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
