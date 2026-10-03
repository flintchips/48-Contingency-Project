using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace OpaliteMoonMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class OpaliteMoonPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "FlintChips.Contingency";
        public const string PluginName = "48_Contingency";
        public const string PluginVersion = "1.3.0";
        
        internal static ManualLogSource Log = null!;
        
        // Static config tracking field
        public static ConfigEntry<bool> ReplaceWithRegularScrap = null!;
        public static ConfigEntry<bool> SoppingZedDogEnabled = null!;

        private void Awake()
        {
            Log = Logger;
            
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (args.Name.Contains("OpaliteMod"))
                {
                    Log.LogDebug("[48contingency] Redirecting OpaliteMod assembally to 48contingency!");
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

        private void LoadMudAssets()
        {
            try
            {
                string modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string bundlePath = Path.Combine(modFolder, "Assets", "muddyassets"); 

                if (File.Exists(bundlePath))
                {
                    AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
                    if (bundle != null)
                    {
                        MudVariantApplier.MudBaseMaterial = bundle.LoadAsset<Material>("MudMaterial");
                        MudVariantApplier.MuddyDropSFX = bundle.LoadAsset<AudioClip>("DropMuddyObject");
                        MudVariantApplier.MuddyPickupSFX = bundle.LoadAsset<AudioClip>("MuddyPickup");

                        Log.LogInfo("[OpaliteMoonMod] Muddy variant assets successfully initialized!");
                    }
                    else
                    {
                        Log.LogError("[OpaliteMoonMod] Found asset bundle file, but failed to load it.");
                    }
                }
                else
                {
                    Log.LogError($"[OpaliteMoonMod] AssetBundle not found at path: {bundlePath}. Muddy variants will be broken.");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[OpaliteMoonMod] Critical error loading asset assets: {ex.Message}");
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
                    Debug.Log($"[SeedCheckPatch] ControlRoomManager not found!");
                }

                Debug.Log($"[SeedCheckPatch] Prefix called before LoadNewLevel(), seed is {currentSeed}");
            }
        }
    }
}