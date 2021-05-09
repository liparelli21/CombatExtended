using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Noise;

namespace CombatExtended.HarmonyCE
{
    /*
     *  Removes the stats in StatsToCull from the ThingDef info screen
     *  Acts as a StatWorker for BurstShotCount (which is normally handled by ThingDef)
     *  Acts as a StatWorker for CoverEffectiveness (which is normally handled by ThingDef)
     */

    [HarmonyPatch]
    internal static class Harmony_ThingDef
    {
        private static readonly string[] StatsToCull = { "ArmorPenetration", "StoppingPower", "Damage" };
        private const string BurstShotStatName = "BurstShotCount";
        private const string CoverStatName = "CoverEffectiveness";

        private static System.Type type;
        private static FieldInfo weaponField;
        private static FieldInfo thisField;
        private static FieldInfo currentField;
        private static FieldInfo valueStringInt = AccessTools.Field(typeof(StatDrawEntry), "valueStringInt");

        static MethodBase TargetMethod()
        {
            type = typeof(ThingDef).GetNestedTypes(AccessTools.all).FirstOrDefault(x => x.Name.Contains("<SpecialDisplayStats>"));
            weaponField = AccessTools.Field(type, AccessTools.GetFieldNames(type).FirstOrDefault(x => x.Contains("<verb>")));
            thisField = AccessTools.Field(type, AccessTools.GetFieldNames(type).FirstOrDefault(x => x.Contains("this")));
            currentField = AccessTools.Field(type, AccessTools.GetFieldNames(type).FirstOrDefault(x => x.Contains("current")));

            return AccessTools.Method(type, "MoveNext");
        }

        public static void Postfix(IEnumerator<StatDrawEntry> __instance, ref bool __result)
        {
            if (__result)
            {
                var entry = __instance.Current;
                if (entry.LabelCap.Contains(BurstShotStatName.Translate().CapitalizeFirst()))
                {
                    var def = (ThingDef)thisField.GetValue(__instance);
                    var compProps = def.GetCompProperties<CompProperties_FireModes>();

                    if (compProps != null)
                    {
                        var aimedBurstCount = compProps.aimedBurstShotCount;
                        var burstShotCount = ((VerbProperties)weaponField.GetValue(__instance)).burstShotCount;

                        // Append aimed burst count
                        if (aimedBurstCount != burstShotCount)
                        {
                            valueStringInt.SetValue(entry, $"{aimedBurstCount} / {burstShotCount}", BindingFlags.NonPublic | BindingFlags.Instance, null, CultureInfo.InvariantCulture);
                        }
                    }
                }
                // Override cover effectiveness with collision height
                else if (entry.LabelCap.Contains(CoverStatName.Translate().CapitalizeFirst()))
                {
                    // Determine collision height
                    var def = (ThingDef)thisField.GetValue(__instance);
                    if (def.plant?.IsTree ?? false)
                        return;

                    var height = def.Fillage == FillCategory.Full
                        ? CollisionVertical.WallCollisionHeight
                        : def.fillPercent;
                    height *= CollisionVertical.MeterPerCellHeight;

                    var newEntry = new StatDrawEntry(entry.category, "CE_CoverHeight".Translate(), height.ToStringByStyle(ToStringStyle.FloatMaxTwo) + " m", (string)"CE_CoverHeightExplanation".Translate(), entry.DisplayPriorityWithinCategory);

                    currentField.SetValue(__instance, newEntry);
                }
                // Remove obsolete vanilla stats
                else if (StatsToCull.Select(s => s.Translate().CapitalizeFirst()).Contains(entry.LabelCap))
                {
                    __result = __instance.MoveNext();
                }
            }
        }
    }

    // To test if it works:
    //      See if the displayed stats in the info card are for the TURRET GUN, rather than for the TURRET BUILDING

    [HarmonyPatch(typeof(ThingDef), "SpecialDisplayStats")]
    static class Harmony_ThingDef_SpecialDisplayStats_Patch
    {
        public static void Postfix(ThingDef __instance, ref IEnumerable<StatDrawEntry> __result, StatRequest req)
        {
            var turretGunDef = __instance.building?.turretGunDef ?? null;

            if (turretGunDef != null)
            {
                var statRequestGun = StatRequest.For(turretGunDef, null);
                
                var cache = __result;
                // :/
                var newStats1 = DefDatabase<StatDef>.AllDefs
                    .Where(x => x.category == StatCategoryDefOf.Weapon
                        && x.Worker.ShouldShowFor(statRequestGun)
                        && !x.Worker.IsDisabledFor(req.Thing)
                        && !(x.Worker is StatWorker_MeleeStats))
                    .Where(x => !cache.Any(y => y.stat == x))
                    .Select(x => new StatDrawEntry(StatCategoryDefOf.Weapon, x, turretGunDef.GetStatValueAbstract(x), statRequestGun, ToStringNumberSense.Undefined))
                    .Where(x => x.ShouldDisplay);
                
                __result = __result.Concat(newStats1);
            }
        }
    }

    [HarmonyPatch(typeof(ThingDef), "PostLoad")]
    static class Harmony_ThingDef_PostLoad_Patch
    {
        [HarmonyPostfix]
        public static void PostFix(ThingDef __instance)
        {
            if (__instance.tools != null)
            {
                var tools = __instance.tools.Where(x => x is ToolCE).Select(x => x as ToolCE);

                foreach (var tool in tools)
                    tool.PostLoadCE(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(ThingDef), "ConfigErrors")]
    static class Harmony_ThingDef_ConfigErrors_Patch
    {
        [HarmonyPostfix]
        public static void PostFix(ThingDef __instance, ref IEnumerable<string> __result)
        {
            if (__instance.race != null && __instance.tools != null)
            {
                var tools = __instance.tools.Where(x => x is ToolCE).Select(x => x as ToolCE);

                bool hasUpperFallback = tools.Any(x => x.UpperFallback);
                bool hasLowerFallback = tools.Any(x => x.LowerFallback);

                if (!hasLowerFallback || !hasUpperFallback)
                {
                    var any = tools.Where(x => x.restrictedReach == MeleeFallback.Automatic && x.ensureLinkedBodyPartsGroupAlwaysUsable);


                    if (!any.Any(x => x.ensureLinkedBodyPartsGroupAlwaysUsable))
                    {
                        bool addedELBPGAU = false;
                        foreach (var tool in tools)
                        {
                            //Get BodyPartDef holding BodyPartRecord
                            BodyPartRecord part = tool.ParentDef.race.body.AllParts.FirstOrDefault(x => x.IsInGroup(tool.linkedBodyPartsGroup));
                            if (part == null || part.def.tags.Any(x => x.vital))
                                continue;

                            var allVitals = tool.ParentDef.race.body.AllParts
                                .Where(x => x.def.tags.Any(y => y.vital) && x.coverageAbs > 0);

                            //See if any vital parts would be damaged if this part were destroyed
                            var vitalChildren = CollisionVertical.Children(part)
                                .Where(x => x.def.tags.Any(y => y.vital) && x.coverageAbs > 0);

                            bool containsVital = false;

                            foreach (var vitalTag in vitalChildren.SelectMany(x => x.def.tags.Where(y => y.vital)))
                            {
                                if (allVitals.Count(x => x.def.tags.Contains(vitalTag))
                                    == vitalChildren.Count(x => x.def.tags.Contains(vitalTag)))
                                {
                                    //ALL vital parts of a certain kind would be damaged if this part were destroyed
                                    containsVital = true;
                                }
                            }

                            if (containsVital)
                            {
                                tool.ensureLinkedBodyPartsGroupAlwaysUsable = true;
                                addedELBPGAU = true;
                            }

                        }
                        //Get PawnCapacityDef:
                        // Verse.Pawn_HealthTracker ShouldBeDeadFromRequiredCapacity()
                        // pawn.RaceProps.IsFlesh ? pawnCapacityDef.lethalFlesh : pawnCapacityDef.lethalMechanoids

                        if (!addedELBPGAU)
                            __result = __result.Append("lacks ensureLinkedBodyPartsGroupAlwaysUsable=True, and this could not be automatically set!!!");
                    }

                    if (any.Any())
                    {
                        bool needBothGenders = any.Any(x => x.restrictedGender == Gender.Male) && any.Any(x => x.restrictedGender == Gender.Female);
                        ToolCE fC;
                        ToolCE sC;

                        bool overallSuccess = false;

                        if (!needBothGenders)
                        {
                            bool noneSuccess = FallbackHandling(any.Where(x => x.restrictedGender == Gender.None), hasLowerFallback, hasUpperFallback, out fC, out sC);
                            if (noneSuccess)
                            {
                                overallSuccess = true;
                            }
                        }
                        else
                        {
                            var male = any.Where(x => x.restrictedGender == Gender.Male || x.restrictedGender == Gender.None);
                            var female = any.Where(x => x.restrictedGender == Gender.Female);
                            bool maleSuccess = FallbackHandling(male, hasLowerFallback, hasUpperFallback, out fC, out sC);
                            if (maleSuccess)
                            {
                                if (fC != null)
                                {
                                    if (fC.restrictedGender == Gender.None)
                                    {
                                        hasLowerFallback = hasLowerFallback || fC.LowerFallback;
                                        hasUpperFallback = hasUpperFallback || fC.UpperFallback;
                                    }
                                }
                                if (sC != null)
                                {
                                    if (sC.restrictedGender == Gender.None)
                                    {
                                        hasLowerFallback = hasLowerFallback || sC.LowerFallback;
                                        hasUpperFallback = hasUpperFallback || sC.UpperFallback;
                                    }
                                }
                                overallSuccess = true;
                            }

                            bool femaleSuccess = FallbackHandling(female, hasLowerFallback, hasUpperFallback, out fC, out sC);
                            if (femaleSuccess)
                            {


                                hasLowerFallback = hasLowerFallback || (fC?.LowerFallback ?? false) || (sC?.LowerFallback ?? false);
                                hasUpperFallback = hasUpperFallback || (fC?.UpperFallback ?? false) || (sC?.UpperFallback ?? false);
                                overallSuccess = true;
                            }
                        }

                        if (!overallSuccess)
                            __result = __result.Append("fallback could not be set! In <tools> please assign <restrictedReach> of one tool to Nearest or FullBody, or <restrictedReach> of one tool to NearestAbove and another to NearestBelow.");

                        //if (overallSuccess) str += ". If this is desired, please set <restrictedReach> to the same value in XML to hide this warning.";
                    }
                    else
                    {
                        __result = __result.Append("none of the tools contain ensureLinkedBodyPartsGroupAlwaysUsable! The pawn will be unable to melee when exposed to enough damage.");
                    }
                }
            }
        }

        static bool FallbackHandling(IEnumerable<ToolCE> tools, bool hasLowerFallback, bool hasUpperFallback, out ToolCE firstChanged, out ToolCE secondChanged)
        {
            firstChanged = null;
            secondChanged = null;

            if (!tools.Any())
                return false;

            var limited = (!hasLowerFallback
                            ? (!hasUpperFallback
                                ? tools.Where(x => x.AttackPartHeight == BodyPartHeight.Top || x.AttackPartHeight == BodyPartHeight.Bottom)
                                : tools.Where(x => x.AttackPartHeight == BodyPartHeight.Bottom))
                            : tools.Where(x => x.AttackPartHeight == BodyPartHeight.Top));
            var singleNeed = (!hasLowerFallback
                        ? (!hasUpperFallback
                            ? MeleeFallback.Nearest
                            : MeleeFallback.NearestBelow)
                        : MeleeFallback.NearestAbove);
            bool needOne = hasLowerFallback || hasUpperFallback;

            if (tools.Count() == 1 || !limited.Any())     //Have to be all on Middle or Undefined
            {
                firstChanged = tools.First();
                firstChanged.restrictedReach = singleNeed;
            }
            else if (needOne)
            {
                firstChanged = limited.First();// OrDefault(x => x.ensureLinkedBodyPartsGroupAlwaysUsable) ?? limited.First();
                firstChanged.restrictedReach = singleNeed;
            }
            else if (!limited.Any(x => x.AttackPartHeight == BodyPartHeight.Top) || !limited.Any(x => x.AttackPartHeight == BodyPartHeight.Bottom))  //There's only one of the needed two
            {
                firstChanged = limited.First();// OrDefault(x => x.ensureLinkedBodyPartsGroupAlwaysUsable) ?? limited.First();
                secondChanged = tools.Except(limited.First()).First();// OrDefault(x => x.ensureLinkedBodyPartsGroupAlwaysUsable) ?? any.Except(limited.First()).First();
                                                                    //any.Count() is above 1, so there are others we could use
                if (firstChanged.AttackPartHeight == BodyPartHeight.Top)
                {
                    firstChanged.restrictedReach = MeleeFallback.NearestAbove;
                    secondChanged.restrictedReach = MeleeFallback.NearestBelow;
                }
                else
                {
                    firstChanged.restrictedReach = MeleeFallback.NearestBelow;
                    secondChanged.restrictedReach = MeleeFallback.NearestAbove;
                }
            }
            else    //There are multiple, and we need two (and limited has two)
            {
                var top = limited.Where(x => x.AttackPartHeight == BodyPartHeight.Top);
                var bottom = limited.Where(x => x.AttackPartHeight == BodyPartHeight.Bottom);
                firstChanged = top.First();// OrDefault(x => x.ensureLinkedBodyPartsGroupAlwaysUsable) ?? top.First();
                firstChanged.restrictedReach = MeleeFallback.NearestAbove;
                secondChanged = bottom.First();// OrDefault(x => x.ensureLinkedBodyPartsGroupAlwaysUsable) ?? bottom.First();
                secondChanged.restrictedReach = MeleeFallback.NearestBelow;
            }

            return firstChanged != null || secondChanged != null;
        }
    }
}