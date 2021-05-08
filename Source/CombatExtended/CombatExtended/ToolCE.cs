using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using RimWorld;
using Verse;

namespace CombatExtended
{
    public class ToolCE : Tool
    {
        ThingDef parentDef;
        ThingDef ParentDef
        {
            get
            {
                if (parentDef == null)
                {
                    //var hediffDef = DefDatabase<HediffDef>.AllDefs.FirstOrDefault(x => x.HasComp(typeof(HediffComp_VerbGiver)) && (x.CompProps<HediffCompProperties_VerbGiver>().tools?.Contains(this) ?? false));
                    //if (hediffDef != null)
                    //    parentDef = hediffDef;
                    parentDef = DefDatabase<ThingDef>.AllDefs.FirstOrDefault(x => x.tools?.Contains(this) ?? false);
                }
                return parentDef;
            }
        }

        public float armorPenetrationSharp;
        public float armorPenetrationBlunt;
        /// <summary>
        /// Absolute added vertical distance (in cells) beyond the attacker's arm length
        /// E.g a knife has reach near to 0, Mace probably ~0.2-0.3 for the head and ~0 for the pommel ...
        /// </summary>
        float reach = -1;
        public float Reach
        {
            get
            {
                if (reach == -1)
                {
                    //Races
                    if (ParentDef == null || ParentDef.race != null)
                        reach = 0;

                    //Weapons
                    bool isOneHanded = ParentDef.weaponTags?.Contains(Apparel_Shield.OneHandedTag) ?? false;
                    //if (parentDef.HasComp(typeof(CompAmmoUser)))

                    //Bulk is calculated from weapon length in the balance sheet (Bulk(L) = L(mm) / 100)
                    //To revert back, L(m) = 0.1 * Bulk(L)
                    //One-handed weapons add their full length, two-handed weapons add half of their length to reach
                    reach = (isOneHanded ? 0.1f : 0.05f) * ParentDef.GetStatValueAbstract(CE_StatDefOf.Bulk);
                }
                return reach;
            }
        }
        public Gender restrictedGender = Gender.None;
        public MeleeFallback restrictedReach = MeleeFallback.Automatic;

        bool partHeightFixed = false;
        BodyPartHeight attackPartHeight = BodyPartHeight.Undefined;
        /// <returns>BodyPartHeight of the attackers' bodypart capable of performing this tool's attack</returns>
        public BodyPartHeight AttackPartHeight
        {
            get
            {
                if (!partHeightFixed)
                {
                    if (ParentDef != null)
                    {
                        if (ParentDef.race == null
                            || linkedBodyPartsGroup == null)
                        {
                            attackPartHeight = BodyPartHeight.Middle;
                        }
                        else
                            attackPartHeight = CollisionVertical.FromRecord(
                                ParentDef.race.body.AllParts.FirstOrDefault(x => x.IsInGroup(linkedBodyPartsGroup)));

                        partHeightFixed = true;
                    }
                }
                return attackPartHeight;
            }
        }

        public bool UpperFallback => restrictedReach == MeleeFallback.NearestAbove || restrictedReach == MeleeFallback.Nearest || restrictedReach == MeleeFallback.FullBody;
        public bool LowerFallback => restrictedReach == MeleeFallback.NearestBelow || restrictedReach == MeleeFallback.Nearest || restrictedReach == MeleeFallback.FullBody;

        public virtual void PostLoadCE(ThingDef parentDef)
        {
            this.parentDef = parentDef;
        }
    }
}
