using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using DuckGame.TS;

namespace DuckGame.Expedition.EditorTools
{
    /// <summary>
    /// Fills TSSprites with the hand-verified key -> sprite table, and TSAudioCatalogue with
    /// the full clip table, then writes TSSoundMap from clips that actually exist.
    ///
    /// Every mapping below was confirmed by looking at the art, not by trusting its filename.
    /// Sprites for unrelated content (console button glyphs, loading spinners, star-sign
    /// portraits, brand plates) are intentionally left unwired.
    /// </summary>
    public static class TSAssetWiring
    {
        const string SpriteDir = "Assets/DuckGame/TSAssets/Sprites";
        const string AudioDir = "Assets/DuckGame/TSAssets/Audio";
        const string RuntimeDir = "Assets/DuckGame/TSAssets/Runtime";
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";
        const string ReportPath = "docs/asset-wiring-report.txt";

        /// <summary>Key -> sprite file name (no extension). Order = documented mapping table.</summary>
        static readonly (string key, string file)[] SpriteMap =
        {
            // --- element glyphs: snowflake / flame / bolt / claw marks ---
            (TSSprites.ElementIce,       "UI_Elements_02_1"),
            (TSSprites.ElementFire,      "UI_Elements_02_0"),
            (TSSprites.ElementLightning, "UI_Elements_02_2"),
            (TSSprites.ElementWitch,     "UI_Elements_02_3"),

            // --- risk: shield=normal, warning triangle=elite, horned skull=boss, ???=unknown ---
            (TSSprites.RiskNormal,       "IconSet_Equip_Shield"),
            (TSSprites.RiskElite,        "UI_Elements_04_0"),
            (TSSprites.RiskBoss,         "function_icon_skull_elite"),
            (TSSprites.RiskUnknown,      "AchievementsAtlas_63"),

            // --- player stats ---
            (TSSprites.StatHealth,       "UI_Elements_02_9"),
            (TSSprites.StatHealthUp,     "UI_Elements_03_10"),
            (TSSprites.StatCoin,         "CatCoin_1x1"),
            (TSSprites.StatKill,         "function_icon_skull"),
            (TSSprites.StatSpeed,        "UI_Elements_02_8"),
            (TSSprites.StatRate,         "UI_Elements_03_11"),
            (TSSprites.StatCount,        "UI_Elements_02_5"),
            (TSSprites.StatChest,        "function_icon_chest_1"),

            // --- upgrade corner badges (element + rising arrow) ---
            (TSSprites.AffixIce,         "UI_Elements_03_1"),
            (TSSprites.AffixFire,        "UI_Elements_03_0"),
            (TSSprites.AffixLightning,   "UI_Elements_03_2"),
            (TSSprites.AffixWitch,       "UI_Elements_03_3"),
            (TSSprites.AffixSpeed,       "UI_Elements_02_8"),
            (TSSprites.AffixRate,        "UI_Elements_05_6"),
            (TSSprites.AffixCount,       "UI_Elements_02_5"),
            (TSSprites.AffixMax,         "UI_Elements_05_0"),
            (TSSprites.AffixTaken,       "CheckMark"),
            (TSSprites.CardMask,         "Card_01_v2_Mask"),

            // --- reward card faces: real spell card art ---
            (TSSprites.CardStorm,        "StormOrb"),
            (TSSprites.CardFire,         "Firestorm"),
            (TSSprites.CardFrost,        "FrostBall"),
            (TSSprites.CardBolt,         "MagicBolt"),
            (TSSprites.CardChakram,      "Chakram"),
            (TSSprites.CardSpread,       "ContagiousSpread"),

            // --- facilities / interaction ---
            (TSSprites.FacilityBank,     "function_icon_bank"),
            (TSSprites.FacilityFly,      "function_icon_boot_fly"),
            (TSSprites.FacilityChest,    "Chest_Open_SparklingCoin_s_05"),
            (TSSprites.FacilityLock,     "Lock"),
            (TSSprites.FacilityUpgrade,  "Upgrade"),
            (TSSprites.FacilityWarning,  "warning"),

            // --- input glyphs (keyboard/mouse only; console glyphs are not used) ---
            (TSSprites.KeyF,             "keyboard_f"),
            (TSSprites.KeyEsc,           "keyboard_escape"),
            (TSSprites.MouseLeft,        "mouse_left"),

            // --- route panel chrome ---
            (TSSprites.RouteBackdrop,    "StarConstellation"),
            (TSSprites.RouteLine,        "StarLine"),
            (TSSprites.RouteSignFrame,   "Constellation_Small_top"),
            (TSSprites.RouteGlow,        "Glow_V2"),

            // --- depth badges are roman numerals II..VI (I is drawn from the numeral II? no:
            //     AchievementsAtlas_75=II, 76=III, 77=IV, 78=V; depth 1 uses the plain bolt-less
            //     numeral set start, which is AchievementsAtlas_71-ish; use 75..78 shifted.)
        };

        /// <summary>
        /// Depth badges, one per depth that actually has a plain roman-numeral glyph.
        /// Verified on the contact sheet: 75=II, 76=III, 77=IV, 78=V, 79=VI.
        /// There is NO standalone "I" glyph (AchievementsAtlas_26 is a lightning bolt with
        /// an I beside it, i.e. an element achievement), so depth 1 stays text-only instead
        /// of borrowing art that means something else.
        /// </summary>
        static readonly (int depth, string file)[] DepthBadges =
        {
            (2, "AchievementsAtlas_75"),
            (3, "AchievementsAtlas_76"),
            (4, "AchievementsAtlas_77"),
            (5, "AchievementsAtlas_78"),
            (6, "AchievementsAtlas_79"),
        };

        /// <summary>Named one-shots: gameplay key -> exact clip file name.</summary>
        static readonly (string key, string clip)[] AudioSingles =
        {
            ("shot.default",   "Fire_Cast_1s_03"),
            ("shot.ice",       "Cold_Cast_1s_01"),
            ("shot.fire",      "Fire_Cast_2s_01"),
            ("shot.lightning", "Lightning_Cast_1s_01"),
            ("shot.witch",     "FairyTale_Cast_1s_01"),

            ("hit.enemy",      "etfx_explosion_magic2"),
            ("hit.world",      "etfx_explosion_flash"),
            ("kill.small",     "Bat_Small_Pop"),
            ("kill.normal",    "Bat_Normal_Pop"),
            ("kill.big",       "Bat_Big_Pop"),

            ("player.hurt",    "TakeDamage"),
            ("player.heal",    "Heal3"),

            ("coin.drop",      "Chest_SpawnDrop"),
            ("chest.open",     "Chest_SpawnParticles"),

            ("ui.confirm",     "Notif"),
            ("ui.hover",       "Ring"),
            ("ui.levelup",     "EditLevelup"),
            ("ui.carddraw",    "SFX_CardDraw"),
            ("ui.cardrefresh", "SFX_CardRefresh"),
            ("ui.cardshuffle", "SFX_CardShuffle"),
            ("ui.elitewarn",   "12 (Monster card draw) v1.0"),

            ("travel.start",   "VFX_Teleport_Start"),
            ("travel.end",     "VFX_Teleport_End"),
            ("portal.start",   "VFX_Portal_Start_audio"),
            ("portal.end",     "VFX_Portal_End_audio"),
            ("portal.loop",    "VFX_Portal_audio"),

            ("music.hub",      "12 (hub) Remake Final"),
            ("music.reward",   "13 (Power up selection) Final"),
            ("music.postcombat","14 (Post combat) Final"),
            ("music.victory",  "Victory v1.0"),
            ("music.defeat",   "Defeat v1.0"),
            ("music.scoring",  "Scoring menu v1.0"),
            ("music.menu",     "11 (menu) Final"),
        };

        [MenuItem("Duck Game/Expedition/Wire Audio And Sprites")]
        public static void Run()
        {
            var report = new StringBuilder();
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                HubScene, UnityEditor.SceneManagement.OpenSceneMode.Single);

            // ================= SPRITES =================
            var spriteEntries = new List<TSSprites.Entry>();
            var spriteKeys = new List<string>();
            var spriteFiles = new List<string>();

            foreach (var (key, file) in SpriteMap)
            {
                var path = SpriteDir + "/" + file + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) { report.AppendLine("SPRITE MISSING: " + key + " -> " + file); continue; }
                spriteEntries.Add(new TSSprites.Entry { key = key, sprite = sprite });
                spriteKeys.Add(key);
                spriteFiles.Add(file);
            }
            for (int d = 0; d < DepthBadges.Length; d++)
            {
                var (depth, file) = DepthBadges[d];
                var path = SpriteDir + "/" + file + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) { report.AppendLine("DEPTH BADGE MISSING: " + file); continue; }
                spriteEntries.Add(new TSSprites.Entry { key = "depth." + depth, sprite = sprite });
            }

            var spriteHost = FindOrCreate(scene, "TS_Sprites");
            var spriteDb = spriteHost.GetComponent<TSSprites>();
            if (spriteDb == null) spriteDb = spriteHost.AddComponent<TSSprites>();
            spriteDb.SetTable(spriteEntries.ToArray());
            EditorUtility.SetDirty(spriteDb);
            report.AppendLine("sprite table = " + spriteEntries.Count + " entries");

            // ================= AUDIO : full table =================
            var audioPaths = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir });
            var clipEntries = new List<TSAudioCatalogue.KeyedClip>();
            var clipNames = new List<string>();
            foreach (var g in audioPaths)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (clip == null) continue;
                string n = Path.GetFileNameWithoutExtension(p);
                clipEntries.Add(new TSAudioCatalogue.KeyedClip { key = n, clip = clip });
                clipNames.Add(n);
            }
            clipEntries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
            clipNames.Sort(string.CompareOrdinal);

            var audioHost = FindOrCreate(scene, "TS_AudioCatalogue");
            var cat = audioHost.GetComponent<TSAudioCatalogue>();
            if (cat == null) cat = audioHost.AddComponent<TSAudioCatalogue>();
            cat.SetTable(clipEntries.ToArray());
            EditorUtility.SetDirty(cat);
            report.AppendLine("audio table = " + clipEntries.Count + " clips");

            // ================= AUDIO : do not keep 750 MB resident =================
            int touched = 0;
            foreach (var entry in clipEntries)
            {
                var path = AssetDatabase.GetAssetPath(entry.clip);
                var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                if (imp == null) continue;
                var s = imp.defaultSampleSettings;
                if (s.preloadAudioData)
                {
                    s.preloadAudioData = false;
                    imp.defaultSampleSettings = s;
                    imp.SaveAndReimport();
                    touched++;
                }
            }
            report.AppendLine("import settings touched = " + touched + " (preload off)");

            // ================= TSSoundMap =================
            WriteSoundMap(clipNames, AudioSingles, report);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, HubScene);
            report.AppendLine("");
            report.AppendLine("SAVED IN PLACE: " + HubScene);

            // ================= verification listing =================
            report.AppendLine("");
            report.AppendLine("=== sprite keys written ===");
            for (int i = 0; i < spriteKeys.Count; i++)
                report.AppendLine("  " + spriteKeys[i] + "  <-  " + spriteFiles[i]);

            File.WriteAllText(Path.GetFullPath(ReportPath), report.ToString());
            Debug.Log("TS_ASSET_WIRING_DONE");
        }

        static void WriteSoundMap(List<string> clipNames, (string key, string clip)[] singles, StringBuilder report)
        {
            var families = new Dictionary<string, List<string>>();
            void Add(string fam, string clip)
            {
                if (!families.TryGetValue(fam, out var l)) { l = new List<string>(); families[fam] = l; }
                if (!l.Contains(clip)) l.Add(clip);
            }

            foreach (var n in clipNames)
            {
                string lower = n.ToLowerInvariant();
                if (lower.Contains("cast")) Add("Cast", n);
                else if (lower.Contains("impact") || lower.Contains("hit")) Add("Impact", n);
                else if (lower.Contains("death") || lower.Contains("screech") || lower.Contains("pop")) Add("Death", n);
                else if (lower.Contains("hover")) Add("Hover", n);
                else if (lower.Contains("chest") || lower.Contains("drop") || lower.Contains("pickup")) Add("Pickup", n);
                else if (lower.Contains("card") || lower.Contains("notif") || lower.Contains("ring") ||
                         lower.Contains("levelup") || lower.Contains("scoring") || lower.Contains("menu"))
                    Add("Ui", n);
                else if (lower.Contains("final")) Add("Music", n);
                else if (lower.Contains("stage")) Add("MusicStage", n);
                else Add("Misc", n);
            }

            var sb = new StringBuilder();
            sb.AppendLine("// AUTO-GENERATED by TSAssetWiring. Do not edit by hand.");
            sb.AppendLine("// Families list only clips that were actually imported.");
            sb.AppendLine("namespace DuckGame.TS");
            sb.AppendLine("{");
            sb.AppendLine("    public static class TSSoundMap");
            sb.AppendLine("    {");
            foreach (var field in new[] { "Cast", "Impact", "Death", "Ui", "Music", "MusicStage", "Pickup", "Hover", "Misc" })
            {
                sb.AppendLine("        public static readonly string[] " + field + " = {");
                if (families.TryGetValue(field, out var l))
                    foreach (var n in l) sb.AppendLine("            \"" + Esc(n) + "\",");
                sb.AppendLine("        };");
            }
            sb.AppendLine();
            sb.AppendLine("        // Named one-shots addressed by gameplay meaning.");
            var seen = new HashSet<string>();
            foreach (var (key, clip) in singles)
            {
                if (string.IsNullOrEmpty(clip)) { report.AppendLine("AUDIO SINGLE MISSING: " + key); continue; }
                if (!clipNames.Contains(clip)) { report.AppendLine("AUDIO SINGLE NOT IMPORTED: " + key + " -> " + clip); continue; }
                string field = FieldName(key);
                if (!seen.Add(field)) continue;
                sb.AppendLine("        public const string " + field + " = \"" + Esc(clip) + "\";");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");

            File.WriteAllText(Path.GetFullPath(RuntimeDir + "/TSSoundMap.cs"), sb.ToString());
            AssetDatabase.Refresh();
            report.AppendLine("sound map written (families + named singles)");
        }

        static string FieldName(string key)
        {
            var parts = key.Split('.');
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                if (p.Length == 0) continue;
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }
            return sb.ToString();
        }

        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        static GameObject FindOrCreate(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go;
                var t = go.transform.Find(name);
                if (t != null) return t.gameObject;
                var deep = FindDeep(go.transform, name);
                if (deep != null) return deep.gameObject;
            }
            return new GameObject(name);
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
