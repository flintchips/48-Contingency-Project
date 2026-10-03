using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Dawn.Utils;

namespace OpaliteMoonMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class OpaliteMoonPlugin : BaseUnityPlugin
    {
        public static OpaliteMoonPlugin Instance;

        public const string PluginGuid = "FlintChips.Contingency";
        public const string PluginName = "48_Contingency";
        public const string PluginVersion = "1.3.0";

        internal static ManualLogSource Log = null!;

        public static ConfigEntry<BoundedRange> BasinScrapRange = null!;
        public static ConfigEntry<bool> CanRemoveDockedApparatus = null!;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            BasinScrapRange = Config.Bind(
                "Basin Scrap Options",
                "Basin | Min/Max Scrap",
                new BoundedRange(7, 10),
                "Min/Max number of muddy scrap items to spawn in the drained basin."
            );

            CanRemoveDockedApparatus = Config.Bind(
                "Control Room",
                "Can Remove Docked Apparatus",
                true,
                "Whether or not you can remove the apparatus after docking it in the control room."
            );

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (args.Name.Contains("OpaliteMod"))
                {
                    Log.LogDebug("[48contingency] Redirecting OpaliteMod assembly to 48contingency!");
                    return Assembly.GetExecutingAssembly();
                }
                return null;
            };

            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }

            LoadMudAssets();

            new Harmony(PluginGuid).PatchAll();
            Logger.LogInfo($"Loaded [{PluginGuid} v{PluginVersion}]");
        }

        /// <summary>
        /// Loads MudMaterial + optional SFX from a plugin-side asset bundle.
        /// Place the file at: BepInEx/plugins/.../Assets/mudmaterial
        /// (or Assets/muddyassets for backwards compatibility).
        /// Bundle should contain a Material named "MudMaterial".
        /// Optional: AudioClips "DropMuddyObject", "MuddyPickup".
        /// </summary>
        private void LoadMudAssets()
        {
            try
            {
                string modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string[] candidates =
                {
                    Path.Combine(modFolder, "Assets", "mudmaterial"),
                    Path.Combine(modFolder, "Assets", "muddyassets"),
                    Path.Combine(modFolder, "mudmaterial"),
                };

                string bundlePath = null;
                foreach (string path in candidates)
                {
                    if (File.Exists(path))
                    {
                        bundlePath = path;
                        break;
                    }
                }

                if (bundlePath == null)
                {
                    Log.LogWarning("[OpaliteMoonMod] No mud material bundle found (tried Assets/mudmaterial, Assets/muddyassets). Basin scrap will spawn without mud visuals.");
                    return;
                }

                AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null)
                {
                    Log.LogError($"[OpaliteMoonMod] Failed to load asset bundle at: {bundlePath}");
                    return;
                }

                MudVariantApplier.MudBaseMaterial = bundle.LoadAsset<Material>("MudMaterial");
                MudVariantApplier.MuddyDropSFX = bundle.LoadAsset<AudioClip>("DropMuddyObject");
                MudVariantApplier.MuddyPickupSFX = bundle.LoadAsset<AudioClip>("MuddyPickup");

                if (MudVariantApplier.MudBaseMaterial != null)
                    Log.LogInfo($"[OpaliteMoonMod] Loaded MudMaterial from {bundlePath}");
                else
                    Log.LogWarning($"[OpaliteMoonMod] Bundle loaded but MudMaterial asset not found inside {bundlePath}");
            }
            catch (Exception ex)
            {
                Log.LogError($"[OpaliteMoonMod] Error loading mud assets: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(RoundManager))]
        [HarmonyPatch("LoadNewLevel")]
        public class SeedCheckPatch
        {
            [HarmonyPrefix]
            static void Prefix(RoundManager __instance)
            {
                int currentSeed = StartOfRound.Instance.randomMapSeed;
                var controlRoomManager = FindFirstObjectByType<ControlRoomManager>();
                if (controlRoomManager != null)
                {
                    controlRoomManager.BeforeLoadNewLevel(currentSeed);
                }
                else
                {
                    Debug.Log("[SeedCheckPatch] ControlRoomManager not found!");
                }

                Debug.Log($"[SeedCheckPatch] Prefix called before LoadNewLevel(), seed is {currentSeed}");
            }
        }
    }
}
