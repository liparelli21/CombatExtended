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
        public float armorPenetrationSharp;
        public float armorPenetrationBlunt;
        /// <summary>
        /// Absolute added vertical distance (in cells) beyond the attacker's arm length
        /// E.g a knife has reach near to 0, Mace probably ~0.2-0.3 for the head and ~0 for the pommel ...
        /// </summary>
        public float reach;
        public Gender restrictedGender = Gender.None;

        /// <param name="attackerPawn">Pawn to check RaceProps.body of for linkedBodyPartsGroup</param>
        /// <returns>BodyPartHeight of the attackers' bodypart capable of performing this tool's attack</returns>
        public BodyPartHeight AttackPartHeight(Pawn attackerPawn)
        {
            if (attackerPawn == null) return BodyPartHeight.Undefined;
            if (linkedBodyPartsGroup == null) return BodyPartHeight.Middle;

            // FIND THE BODYPART THAT HANLDES THIS TOOL
            var part = attackerPawn.RaceProps.body.AllParts.FirstOrDefault(x => x.IsInGroup(linkedBodyPartsGroup));

            if (part == null || part.IsCorePart) return BodyPartHeight.Middle;

            // Find the height of the latest parent attached to the corepart (Torso)
            // We use this form, because:
            //     - Torso(Middle)-Legs(Bottom)
            //     - Torso(Middle)-Shoulders-Arm-Hand-Finger(Bottom)
            //     - Torso(Middle)-Neck(Top)
            // You want to select Legs/Neck as Bottom/Top, but Finger (the only LeftHand BodyPartGroups) to be Middle even though it's assigned Bottom.
            // Therefore: 
            var prevPart = part;
            var parent = part.parent;
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
    }
}
