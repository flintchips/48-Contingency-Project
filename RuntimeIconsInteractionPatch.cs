using HarmonyLib;
using UnityEngine;

namespace OpaliteMoonMod.Patches
{
    [HarmonyPatch(typeof(GrabbableObject))]
    public class RuntimeIconsInteractionPatch
    {
        [HarmonyPatch("GrabItem")]
        [HarmonyPostfix]
        private static void PostfixGrab(GrabbableObject __instance)
        {
            RefreshItemIconCache(__instance);
        }

        [HarmonyPatch("PocketItem")]
        [HarmonyPostfix]
        private static void PostfixPocket(GrabbableObject __instance)
        {
            RefreshItemIconCache(__instance);
        }

        private static void RefreshItemIconCache(GrabbableObject item)
        {
            if (item == null || item.itemProperties == null) return;

            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.github.lethalcompanymodding.runtimeicons"))
            {
                try
                {
                    System.Type iconGeneratorType = System.Type.GetType("RuntimeIcons.IconGenerator, RuntimeIcons");
                    if (iconGeneratorType != null)
                    {
                        var cacheField = iconGeneratorType.GetField("iconCache", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (cacheField != null)
                        {
                            var cacheDictionary = cacheField.GetValue(null) as System.Collections.IDictionary;
                            if (cacheDictionary != null && cacheDictionary.Contains(item.itemProperties))
                            {
                                
                                cacheDictionary.Remove(item.itemProperties);
                            }
                        }
                    }
                }
                catch { 
                }
            }
        }
    }
}