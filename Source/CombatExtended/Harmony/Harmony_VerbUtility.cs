using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace CombatExtended.HarmonyCE
{
    [HarmonyPatch(typeof(VerbUtility), "GetProjectile")]
    internal static class Harmony_VerbUtility
    {
        internal static bool Prefix(Verb verb, ref ThingDef __result)
        {
            if (verb is Verb_LaunchProjectileCE verbCE)
            {
                __result = verbCE.Projectile;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(VerbUtility), "FinalSelectionWeight")]
    internal static class Harmony_VerbUtility_FinalSelectionWeight
    {
        internal static bool Prefix(Verb verb, Pawn p, List<Verb> allMeleeVerbs, float highestWeight, ref float __result)
        {
            VerbSelectionCategory selectionCategory = verb.GetSelectionCategory(p, highestWeight);
            if (selectionCategory == VerbSelectionCategory.Worst)
            {
                int num = 0;
                foreach (Verb allMeleeVerb in allMeleeVerbs)
                {
                    if (allMeleeVerb.GetSelectionCategory(p, highestWeight) == selectionCategory)
                    {
                        num++;
                    }
                }
                __result = 1f / (float)num * 0.00001f;
                return false;
            }
            return true;
        }
    }
}
