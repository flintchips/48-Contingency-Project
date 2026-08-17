using System.Reflection;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace OpaliteMoonMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class OpaliteMoonPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.flintchips.48Contingency";
        public const string PluginName = "48contingency";
        public const string PluginVersion = "0.0.5";
        
        internal static ManualLogSource Log = null!;

        private void Awake()
        {
            Log = Logger;
            
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (args.Name.Contains("OpaliteMod"))
                {
                    Log.LogDebug("[48contingency] Redirecting OpaliteMod to 48contingency!");
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
            
            new Harmony(PluginGuid).PatchAll();
            Logger.LogInfo($"Loaded [Contingency Scripts {PluginVersion}]");
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

