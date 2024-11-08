﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using Verse;
using Verse.Sound;

namespace CombatExtended
{
    public abstract class VerbCIWS : Verb_ShootCE_CIWS, ITargetSearcher
    {
        protected bool debug;
        protected bool holdFire;

        public VerbProperties_CIWS Props => verbProps as VerbProperties_CIWS;
        protected abstract string HoldLabel { get; }
        protected abstract string HoldDesc { get; }
        protected virtual string HoldIcon => "UI/Commands/HoldFire";

        //public override IEnumerable<Gizmo> CompGetGizmosExtra()
        //{
        //    foreach (var gizmo in base.CompGetGizmosExtra())
        //    {
        //        yield return gizmo;
        //    }
        //    if (Turret.CanToggleHoldFire)
        //    {
        //        yield return new Command_Toggle
        //        {
        //            defaultLabel = HoldLabel.Translate(),
        //            defaultDesc = HoldDesc.Translate(),
        //            icon = ContentFinder<Texture2D>.Get(HoldIcon, true),
        //            hotKey = KeyBindingDefOf.Misc6,
        //            toggleAction = delegate ()
        //            {
        //                this.holdFire = !this.holdFire;
        //                if (this.holdFire && HasTarget)
        //                {
        //                    Turret.ResetForcedTarget();
        //                }
        //            },
        //            isActive = (() => this.holdFire)
        //        };
        //    }
        //}
        public virtual bool Active => !holdFire && Turret.Active;
        protected override bool LockRotationAndAngle => false;
        public abstract bool TryFindNewTarget(out LocalTargetInfo target);
        public virtual void ShowTrajectories()
        {
            if (lastShootLine != null)
            {
                Caster.Map.debugDrawer.FlashLine(lastShootLine.Value.source, lastShootLine.Value.Dest, 60, SimpleColor.Green);
            }
        }
        protected (Vector2 firstPos, Vector2 secondPos) PositionOfCIWSProjectile(int sinceTicks, Vector2 destination, bool drawPos = false)
        {
            var firstPos = Caster.Position.ToVector3Shifted();
            var secondPos = firstPos;
            var originV3 = firstPos;
            var originV2 = new Vector2(originV3.x, originV3.z);
            var enumeration = projectilePropsCE.TrajectoryWorker.NextPositions(currentTarget, shotRotation, shotAngle, projectilePropsCE.Gravity, originV2, this.Caster.Position.ToVector3Shifted(), destination, maximumPredectionTicks, maximumPredectionTicks, ShotHeight, false, Vector3.zero, ShotSpeed, originV3, -1f, -1f, -1f, -1f, ShotSpeed, 0).GetEnumerator();
            for (int i = 1; i <= sinceTicks; i++)
            {
                firstPos = secondPos;

                if (!enumeration.MoveNext())
                {
                    break;
                }
                secondPos = enumeration.Current;

            }
            if (drawPos)
            {
                firstPos = projectilePropsCE.TrajectoryWorker.ExactPosToDrawPos(firstPos, sinceTicks - 1, projectilePropsCE.TickToTruePos, Projectile.Altitude);
                secondPos = projectilePropsCE.TrajectoryWorker.ExactPosToDrawPos(secondPos, sinceTicks, projectilePropsCE.TickToTruePos, Projectile.Altitude);
            }
            return (new Vector2(firstPos.x, firstPos.z), new Vector2(secondPos.x, secondPos.z));
        }

        public override bool TryCastShot()
        {
            var result = base.TryCastShot();
            if (result && debug)
            {
                ShowTrajectories();
            }
            return result;
        }
    }
    public abstract class VerbCIWS<TargetType> : VerbCIWS where TargetType : Thing
    {

        public override bool TryFindNewTarget(out LocalTargetInfo target)
        {
            float range = this.verbProps.range;
            var _target = Targets.Where(x => Props.Interceptable(x.def) && !Turret.IgnoredDefs.Contains(x.def)).Where(x => !IsFriendlyTo(x)).FirstOrDefault(t =>
            {
                var verb = this;
                if (Caster.Map.GetComponent<TurretTracker>().CIWS.Any(turret => turret.currentTargetInt.Thing == t) || ProjectileCE_CIWS.ProjectilesAt(Caster.Map).Any(x => x.intendedTarget.Thing == t))
                {
                    return false;
                }
                float num = verb.verbProps.EffectiveMinRange(t, this.Caster);
                if (!verb.TryFindCEShootLineFromTo(Caster.Position, t, out var shootLine))
                {
                    return false;
                }
                var intersectionPoint = shootLine.Dest;
                float num2 = intersectionPoint.DistanceToSquared(Caster.Position);
                return num2 > num * num && num2 < range * range;
            });
            if (_target != null)
            {
                target = _target;
                return true;
            }
            target = null;
            return false;
        }
        protected virtual bool IsFriendlyTo(TargetType thing) => !thing.HostileTo(Caster);
        public abstract IEnumerable<TargetType> Targets { get; }
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true) => target.Thing is TargetType && TryFindCEShootLineFromTo(Caster.Position, target, out _) && base.ValidateTarget(target, showMessages);
        protected abstract IEnumerable<Vector3> TargetNextPositions(TargetType target);
        public override bool TryFindCEShootLineFromTo(IntVec3 root, LocalTargetInfo targetInfo, out ShootLine resultingLine)
        {
            if (base.TryFindCEShootLineFromTo(root, targetInfo, out resultingLine))
            {
                return true;
            }
            if (!(targetInfo.Thing is TargetType target))
            {
                return false;
            }
            var midBurst = numShotsFired > 0;
            var originV3 = Caster.Position.ToVector3Shifted();
            var ticksToSkip = this.BurstWarmupTicksLeft;
            var instant = projectilePropsCE.isInstant;
            if (instant)
            {
                var to = TargetNextPositions(target).Skip(ticksToSkip).FirstOrFallback(Vector3.negativeInfinity);
                if (to == Vector3.negativeInfinity)
                {
                    resultingLine = default;
                    return false;
                }
                resultingLine = new ShootLine(originV3.ToIntVec3(), to.ToIntVec3());
                return true;
            }
            int i = 1;
            var report = ShiftVecReportFor(targetInfo);

            var targetPos1 = new Vector2(target.DrawPos.x, target.DrawPos.z);
            foreach (var pos in TargetNextPositions(target).Skip(ticksToSkip))
            {
                if (i > maximumPredectionTicks)
                {
                    break;
                }
                ShiftTarget(report, false, instant, midBurst, i);

                Vector2 originV2 = new Vector2(originV3.x, originV3.z), destinationV2 = new Vector2(pos.x, pos.z);

                var positions = PositionOfCIWSProjectile(i, destinationV2);
                if (positions.firstPos == positions.secondPos)
                {
                    resultingLine = default(ShootLine);
                    return false;
                }
                Vector2 ciwsPos1 = positions.firstPos, ciwsPos2 = positions.secondPos, targetPos2 = new Vector2(pos.x, pos.z);

                if (CE_Utility.TryFindIntersectionPoint(ciwsPos1, ciwsPos2, targetPos1, targetPos2, out var point))
                {
                    resultingLine = new ShootLine(Shooter.Position, point.ToVector3().ToIntVec3());

                    this.sinceTicks = i;
                    return true;
                }
                targetPos1 = targetPos2;
                i++;

            }
            resultingLine = default;
            return false;
        }
    }
    public abstract class VerbCIWS_Comp<TargetType> : VerbCIWS<Thing> where TargetType : CompCIWSTarget
    {
        public override IEnumerable<Thing> Targets => CompCIWSTarget.Targets<TargetType>(Caster.Map);
        protected override bool IsFriendlyTo(Thing thing) => thing.TryGetComp<TargetType>()?.IsFriendlyTo(thing) ?? base.IsFriendlyTo(thing);
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true) => target.HasThing && target.Thing.HasComp<TargetType>() && base.ValidateTarget(target, showMessages);
        public override bool TryFindCEShootLineFromTo(IntVec3 root, LocalTargetInfo targ, out ShootLine resultingLine)
        {
            if (targ.Thing?.TryGetComp<TargetType>()?.CalculatePointForPreemptiveFire(Projectile, root.ToVector3Shifted(), out var result, BurstWarmupTicksLeft) ?? false)
            {
                resultingLine = new ShootLine(root, result.ToIntVec3());
                return true;
            }
            resultingLine = default;
            return false;
        }
    }

    public abstract class VerbProperties_CIWS : VerbPropertiesCE
    {
        public List<ThingDef> ignored = new List<ThingDef>();
        public IEnumerable<ThingDef> Ignored => ignored;
        public virtual bool Interceptable(ThingDef targetDef) => true;

        private IEnumerable<ThingDef> allTargets;

        public IEnumerable<ThingDef> AllTargets => allTargets ??= InitAllTargets();
        protected abstract IEnumerable<ThingDef> InitAllTargets();
    }
}
