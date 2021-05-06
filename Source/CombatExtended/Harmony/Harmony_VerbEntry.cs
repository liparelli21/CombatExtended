using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CombatExtended.HarmonyCE
{
    [HarmonyPatch(typeof(VerbEntry), "GetSelectionWeight", new Type[] { typeof(Thing) })]
    static class Harmony_VerbEntry
    {
        [HarmonyPostfix]
        public static void PostFix(Thing target, VerbEntry __instance, ref float __result)
        {
            if (__result == 0.0 && __instance.verb.IsUsableOn(target))
            {
                //Small value to make it possible to be picked, but very unlikely unless necessary
                __result = 0.0001f;
            }
        }
    }
}
