using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using DuckGame;
using DuckGame.Expedition;
using DuckGame.TS;

namespace DuckGame.Expedition.EditorTools
{
    /// <summary>
    /// Verifies that the imported ART and AUDIO are actually live in the running game.
    ///
    /// The failure this guards against is real history: TSAudioCatalogue had 165 clips
    /// imported and a Play() method, but nothing ever filled its table, so the game was
    /// silent and looked fine. A report that only says "catalogue exists" cannot catch that,
    /// so this counts LoadedCount, PlayedCount and MissingPlays after driving real events.
    ///
    /// Enabled with -asset-verify.
    /// </summary>
    [InitializeOnLoad]
    public static class AssetVerify
    {
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";
        const string ReportPath = "docs/asset-verify.txt";
        const string DoneMarker = "docs/.asset-verify.done";

        static bool engaged;
        static readonly StringBuilder log = new StringBuilder();
        static bool running;

        static AssetVerify()
        {
            bool requested = false;
            var argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length; i++)
                if (argv[i] == "-asset-verify") { requested = true; break; }
            if (!requested || engaged) return;
            engaged = true;
            EditorApplication.delayCall += Begin;
        }

        static void Begin()
        {
            try
            {
                if (EditorApplication.isPlaying)
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        Path.GetFileNameWithoutExtension(HubScene));
                else
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        HubScene, UnityEditor.SceneManagement.OpenSceneMode.Single);
                    EditorApplication.isPlaying = true;
                }
            }
            catch (Exception e) { Write("begin failed: " + e); return; }

            // Entering play mode reloads the scene, which would destroy a runner created now.
            // Wait until the play session is actually live, then spawn the runner and keep it
            // across the (possible) further scene loads.
            EditorApplication.update += SpawnRunnerWhenPlaying;
        }

        static void SpawnRunnerWhenPlaying()
        {
            if (!EditorApplication.isPlaying) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (UnityEngine.Object.FindObjectOfType<Runner>() != null)
            {
                EditorApplication.update -= SpawnRunnerWhenPlaying;
                return;
            }

            EditorApplication.update -= SpawnRunnerWhenPlaying;
            var host = new GameObject("AssetVerifyRunner");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>();
        }

        sealed class Runner : MonoBehaviour
        {
            void Start() { if (!running) StartCoroutine(Drive()); }

            IEnumerator Drive()
            {
                running = true;
                yield return new WaitForSeconds(1.5f);

                var cat = TSAudioCatalogue.Instance;
                var spr = TSSprites.Instance;
                var run = ExpeditionRun.Instance;
                var rw = RewardSystem.Instance;
                var combat = CombatBridge.Instance;

                log.AppendLine("=== stage 1: tables ===");
                log.AppendLine("TSAudioCatalogue       = " + (cat != null));
                log.AppendLine("audio table entries    = " + (cat != null ? cat.TableCount : -1));
                log.AppendLine("audio clips in memory  = " + (cat != null ? cat.LoadedCount : -1));
                log.AppendLine("TSSprites              = " + (spr != null));
                log.AppendLine("sprite entries         = " + (spr != null ? spr.Count : -1));

                if (spr != null)
                {
                    log.AppendLine("");
                    log.AppendLine("=== stage 2: every documented sprite key resolves ===");
                    var keys = new[]
                    {
                        TSSprites.ElementIce, TSSprites.ElementFire, TSSprites.ElementLightning, TSSprites.ElementWitch,
                        TSSprites.RiskNormal, TSSprites.RiskElite, TSSprites.RiskBoss, TSSprites.RiskUnknown,
                        TSSprites.StatHealth, TSSprites.StatCoin, TSSprites.StatKill,
                        TSSprites.AffixIce, TSSprites.AffixFire, TSSprites.AffixLightning, TSSprites.AffixWitch,
                        TSSprites.CardStorm, TSSprites.CardFire, TSSprites.CardFrost, TSSprites.CardBolt,
                        TSSprites.CardChakram, TSSprites.CardSpread,
                        TSSprites.FacilityBank, TSSprites.FacilityFly, TSSprites.FacilityLock, TSSprites.FacilityWarning,
                        TSSprites.KeyF, TSSprites.MouseLeft,
                        TSSprites.RouteBackdrop, TSSprites.RouteLine, TSSprites.RouteSignFrame, TSSprites.RouteGlow,
                    };
                    int ok = 0, bad = 0;
                    foreach (var k in keys)
                    {
                        if (spr.Has(k)) ok++;
                        else { bad++; log.AppendLine("  MISSING: " + k); }
                    }
                    log.AppendLine("resolved = " + ok + " / " + keys.Length + "   missing = " + bad);
                }

                // ---- stage 3: HUD actually built icon Images ----
                log.AppendLine("");
                log.AppendLine("=== stage 3: HUD icon widgets ===");
                var canvas = FindObjectOfType<ExpeditionHUD>();
                int iconImages = 0, iconsEnabled = 0;
                var anyIcons = new StringBuilder();
                foreach (var img in FindObjectsOfType<Image>())
                {
                    if (img == null || img.gameObject.name == null) continue;
                    if (!img.gameObject.name.StartsWith("icon_")) continue;
                    iconImages++;
                    if (img.enabled && img.sprite != null) { iconsEnabled++; anyIcons.Append(img.gameObject.name + " "); }
                }
                log.AppendLine("HUD present           = " + (canvas != null));
                log.AppendLine("icon Images created    = " + iconImages);
                log.AppendLine("icons showing a sprite = " + iconsEnabled);
                log.AppendLine("which: " + anyIcons.ToString());

                // ---- stage 4: audio actually plays during real events ----
                log.AppendLine("");
                log.AppendLine("=== stage 4: live audio ===");
                int played0 = cat != null ? cat.PlayedCount : -1;
                int missing0 = cat != null ? cat.MissingPlays : -1;

                // 4a: a real shot through the pooled path
                var shooter = FindObjectOfType<OrbShooter>();
                if (shooter != null)
                {
                    bool oldInject = shooter.InjectInput;
                    shooter.InjectInput = true;
                    shooter.ResetSimulation();
                    shooter.TestFire = true;
                    float t = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t < 2.5f) { shooter.TestFire = true; yield return null; }
                    shooter.TestFire = false;
                    shooter.InjectInput = oldInject;
                }
                yield return new WaitForSeconds(.3f);
                int playedShots = cat != null ? cat.PlayedCount : -1;

                // 4b: a real kill through the orb interface
                var brains = FindObjectsOfType<EnemyBrain>();
                if (brains.Length > 0 && combat != null)
                {
                    var b = brains[0];
                    var shot = new OrbShotContext(Vector3.zero, Vector3.forward, 0, null);
                    int guard = 0;
                    while (!b.IsDead && guard++ < 20)
                        b.ReceiveOrbHit(shot, new OrbImpactContext(b.GetComponent<Collider>(), b.transform.position, Vector3.up, true));
                    yield return new WaitForSeconds(.3f);
                }

                // 4c: the reward screen (card shuffle + selection music)
                if (rw != null && run != null && run.Current != null)
                {
                    run.CompleteIsland();
                    rw.OfferFor(run.Current, run.Current.element);
                    yield return new WaitForSeconds(.4f);
                }

                // 4d: music director should have switched tracks
                var music = FindObjectOfType<MusicDirector>();
                var src = cat != null ? cat.Source : null;
                log.AppendLine("MusicDirector present = " + (music != null));
                log.AppendLine("music clip assigned   = " + (src != null && src.clip != null ? src.clip.name : "NONE"));
                log.AppendLine("music is playing      = " + (src != null && src.isPlaying));

                int played1 = cat != null ? cat.PlayedCount : -1;
                int missing1 = cat != null ? cat.MissingPlays : -1;

                log.AppendLine("");
                log.AppendLine("played before shots   = " + played0);
                log.AppendLine("played after shots    = " + playedShots);
                log.AppendLine("played after kill     = " + played1);
                log.AppendLine("missing plays (total) = " + missing1 + "  (before = " + missing0 + ")");
                if (cat != null && cat.MissingPlays > 0)
                    log.AppendLine("last missing key      = " + cat.LastMissingKey);

                // ---- verdict ----
                bool okClips = cat != null && cat.LoadedCount >= 150;
                bool okSpriteTable = spr != null && spr.Count >= 45;
                bool okIcons = iconsEnabled >= 5;
                bool okPlayed = played1 > played0;
                bool okNoMissing = cat != null && cat.MissingPlays == missing0;

                log.AppendLine("");
                log.AppendLine("=== verdict ===");
                log.AppendLine("165 clips registered        = " + okClips + "  (" + (cat != null ? cat.LoadedCount : -1) + ")");
                log.AppendLine("sprite table populated      = " + okSpriteTable + "  (" + (spr != null ? spr.Count : -1) + ")");
                log.AppendLine("HUD icons showing art       = " + okIcons + "  (" + iconsEnabled + ")");
                log.AppendLine("audio played during events  = " + okPlayed);
                log.AppendLine("no missing-clip lookups     = " + okNoMissing);
                bool pass = okClips && okSpriteTable && okIcons && okPlayed && okNoMissing;
                log.AppendLine("PASS                        = " + pass);

                Write("");
                yield return null;
                EditorApplication.isPlaying = false;
                yield return new WaitForSeconds(.5f);
                EditorApplication.Exit(pass ? 0 : 1);
            }
        }

        static void Write(string extra)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ReportPath)));
                var sb = new StringBuilder(log.ToString());
                if (!string.IsNullOrEmpty(extra)) sb.AppendLine(extra);
                File.WriteAllText(Path.GetFullPath(ReportPath), sb.ToString());
                File.WriteAllText(Path.GetFullPath(DoneMarker), "done");
                Debug.Log("ASSET_VERIFY_DONE");
            }
            catch (Exception e) { Debug.LogError("asset verify write failed: " + e.Message); }
        }
    }
}
