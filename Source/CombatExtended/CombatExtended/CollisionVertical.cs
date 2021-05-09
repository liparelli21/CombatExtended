using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public struct CollisionVertical
    {
    	public const float ThickRoofThicknessMultiplier = 2f;
    	public const float NaturalRoofThicknessMultiplier = 2f;
        public const float MeterPerCellHeight = 1.75f;
    	public const float WallCollisionHeight = 2f;       // Walls are this tall
        public const float BodyRegionBottomHeight = 0.45f;  // Hits below this percentage will impact the corresponding body region
        public const float BodyRegionMiddleHeight = 0.85f;  // This also sets the altitude at which pawns hold their guns

        private readonly FloatRange heightRange;
        public readonly float shotHeight;

        public FloatRange HeightRange => new FloatRange(heightRange.min, heightRange.max);
        public float Min => heightRange.min;
        public float Max => heightRange.max;
        public float BottomHeight => Min + heightRange.Span * BodyRegionBottomHeight;
        public float MiddleHeight => Min + heightRange.Span * BodyRegionMiddleHeight;

        public CollisionVertical(Thing thing)
        {
            CalculateHeightRange(thing, thing?.Position ?? default, out heightRange, out shotHeight);
        }
        
        public CollisionVertical(Thing thing, IntVec3 position)
        {
            CalculateHeightRange(thing, position, out heightRange, out shotHeight);
        }

        private static void CalculateHeightRange(Thing thing, IntVec3 position, out FloatRange heightRange, out float shotHeight)
        {
            shotHeight = 0;
            heightRange = new FloatRange(0, 0);
            if (thing == null)
            {
                return;
            }
            
            var plant = thing as Plant;
            if (plant != null)
            {
            		//Height matches up exactly with visual size
            	heightRange = new FloatRange(0f, BoundsInjector.ForPlant(plant).y);
                return;
            }
            
            if (thing is Building)
            {
                if (thing is Building_Door door && door.Open)
            	{
            		return;		//returns heightRange = (0,0) & shotHeight = 0. If not open, doors have FillCategory.Full so returns (0, WallCollisionHeight)
            	}
            	
                if (thing.def.Fillage == FillCategory.Full)
                {
                    heightRange = new FloatRange(0, WallCollisionHeight);
                    shotHeight = WallCollisionHeight;
                    return;
                }
                float fillPercent = thing.def.fillPercent;
                heightRange = new FloatRange(Mathf.Min(0f, fillPercent), Mathf.Max(0f, fillPercent));
                shotHeight = fillPercent;
                return;
            }
            
            float collisionHeight = 0f;
            float shotHeightOffset = 0;
            var pawn = thing as Pawn;
            if (pawn != null)
            {
            	collisionHeight = CE_Utility.GetCollisionBodyFactors(pawn).y;
            	
                shotHeightOffset = collisionHeight * (1 - BodyRegionMiddleHeight);
				
                // Humanlikes in combat crouch to reduce their profile
                if (pawn.IsCrouching())
                {
                    float crouchHeight = BodyRegionBottomHeight * collisionHeight;  // Minimum height we can crouch down to
                    
                    // Find the highest adjacent cover
                    Map map = pawn.Map;
                    foreach (IntVec3 curCell in GenAdjFast.AdjacentCells8Way(position))
                    {
                        if (curCell.InBounds(map))
                        {
                            Thing cover = curCell.GetCover(map);
                            if (cover != null && cover.def.Fillage == FillCategory.Partial && !cover.IsPlant())
                            {
                                var coverHeight = new CollisionVertical(cover).Max;
                                if (coverHeight > crouchHeight) crouchHeight = coverHeight;
                            }
                        }
                    }
                    collisionHeight = Mathf.Min(collisionHeight, crouchHeight + 0.01f + shotHeightOffset);  // We crouch down only so far that we can still shoot over our own cover and never beyond our own body size
                }
            }
            else
            {
                collisionHeight = thing.def.fillPercent;
            }
            var edificeHeight = 0f;
            if (thing.Map != null)
            {
                var edifice = position.GetCover(thing.Map);
                if (edifice != null && edifice.GetHashCode() != thing.GetHashCode() && !edifice.IsPlant())
                {
                    edificeHeight = new CollisionVertical(edifice).heightRange.max;
                }
            }
            float fillPercent2 = collisionHeight;
            heightRange = new FloatRange(Mathf.Min(edificeHeight, edificeHeight + fillPercent2), Mathf.Max(edificeHeight, edificeHeight + fillPercent2));
            shotHeight = heightRange.max - shotHeightOffset;
        }

        /// <summary>
        /// Calculates the BodyPartHeight based on how high a projectile impacted in relation to overall pawn height.
        /// </summary>
        /// <param name="projectileHeight">The height of the projectile at time of impact.</param>
        /// <returns>BodyPartHeight between Bottom and Top.</returns>
        public BodyPartHeight GetCollisionBodyHeight(float projectileHeight, bool ignoreBounds = true)
        {
            if (!ignoreBounds && projectileHeight < Min) return BodyPartHeight.Undefined;
            else if (projectileHeight < BottomHeight) return BodyPartHeight.Bottom;
            else if (projectileHeight < MiddleHeight) return BodyPartHeight.Middle;
            else if (ignoreBounds || projectileHeight < Max) return BodyPartHeight.Top;
            else return BodyPartHeight.Undefined;
        }

        public FloatRange RangeRegion(BodyPartHeight region)
        {
            if (region == BodyPartHeight.Bottom)
                return new FloatRange(Min, BottomHeight);
            else if (region == BodyPartHeight.Middle)
                return new FloatRange(BottomHeight, MiddleHeight);
            else if (region == BodyPartHeight.Top)
                return new FloatRange(MiddleHeight, Max);
            else
                return new FloatRange(0, 0);
        }

        public float WeightForRegion(FloatRange range, BodyPartHeight region)
        {
            var regionRange = RangeRegion(region);
            return (Mathf.Min(range.max, regionRange.max) - Mathf.Max(range.min, regionRange.min)) / regionRange.Span;
        }

        public static BodyPartHeight FromRecord(BodyPartRecord record)
        {
            //var part = parentThingDef.race.body.AllParts.FirstOrDefault(x => x.IsInGroup(linkedBodyPartsGroup));

            if (record == null || record.IsCorePart)
                return BodyPartHeight.Middle;

            // Find the height of the latest parent attached to the corepart (Torso)
            // We use this form, because:
            //     - Torso(Middle)-Legs(Bottom)
            //     - Torso(Middle)-Shoulders-Arm-Hand-Finger(Bottom)
            //     - Torso(Middle)-Neck(Top)
            // You want to select Legs/Neck as Bottom/Top, but Finger (the only LeftHand BodyPartGroups) to be Middle even though it's assigned Bottom.
            // Therefore: 
            var prevPart = record;
            var parent = record.parent;
            while (!parent.IsCorePart)
            {
                prevPart = parent;
                parent = parent.parent;
            }

            //If the latest parent attached to the torso has a height defined, use that
            if (prevPart.height != BodyPartHeight.Undefined)
                return prevPart.height;
            else
                return BodyPartHeight.Middle;

        }

        public static IEnumerable<BodyPartRecord> Children(BodyPartRecord record)
        {
            yield return record;
            int i = 0;
            while (i < record.parts.Count)
            {
                foreach (var child in Children(record.parts[i]))
                {
                    yield return child;
                }
                i++;
            }
        }
    }
}
