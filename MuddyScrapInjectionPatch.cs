using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Object = System.Object;

namespace OpaliteMoonMod.Patches
{
    [HarmonyPatch(typeof(GrabbableObject))]
    public class MuddyScrapInjectionPatch
    {
        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void InjectMudComponent(GrabbableObject __instance)
        {
            if (__instance == null || !__instance.itemProperties.isScrap) return;
            if (__instance.GetComponent<MudVariantApplier>() != null) return;

            bool dogDisabled = OpaliteMoonPlugin.SoppingZedDogEnabled != null && !OpaliteMoonPlugin.SoppingZedDogEnabled.Value;
            bool replaceWithRegularScrap = OpaliteMoonPlugin.ReplaceWithRegularScrap != null && OpaliteMoonPlugin.ReplaceWithRegularScrap.Value;
            
            bool shouldBeMuddy = dogDisabled || replaceWithRegularScrap;

            if (shouldBeMuddy)
            {
                GameObject parentSpawns = GameObject.Find("BasinScrapSpawns");
                if (parentSpawns != null)
                {
                    Transform[] childNodes = parentSpawns.GetComponentsInChildren<Transform>();
                    foreach (Transform node in childNodes)
                    {
                        if (node == parentSpawns.transform) continue;

                        if (Vector3.Distance(__instance.transform.position, node.position) <= 15f)
                        {
                            __instance.gameObject.AddComponent<MudVariantApplier>();
                            Debug.Log($"[OpaliteMoonMod Patch] Intercepted item {__instance.itemProperties.itemName} near basin {node.name}! Muddying.");
                            break;
                        }
                    }
                }
            }
        }
    }
}