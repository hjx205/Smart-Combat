using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using PocketSand;
using Verse;
using Verse.AI;
using UnityEngine;
using HarmonyLib;

namespace Hjx_SmartCombat
{
    [StaticConstructorOnStartup]
    public class StartUp
    {
        static StartUp()
        {
            Log.Message("Smart Combat v1.0");
            var harmony = new Harmony("Hjx.SmartCombat");
            harmony.PatchAll();
        }
    }

    public class Command_AutoChangeWeapeon : Command_Action
    {
        public Pawn pawn;

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions => pawn.equipment.Primary == null ? GetGizmoContextMenuOptions(pawn, "All") : pawn.equipment.Primary.def.IsMeleeWeapon ? GetGizmoContextMenuOptions(pawn, "Ranged") : GetGizmoContextMenuOptions(pawn, "Melee");

        public IEnumerable<FloatMenuOption> GetGizmoContextMenuOptions(Pawn pawn, string flag)
        {
            PawnComp_SmartCombat pawnComp_SmartCombat = pawn.GetComp<PawnComp_SmartCombat>();
            if (pawnComp_SmartCombat == null || pawnComp_SmartCombat.disable)
                yield break;
            Pawn_SmartCombatTracker tracker = pawnComp_SmartCombat.tracker;
            IEnumerable<ThingWithComps> pawnWeapons = pawn.EnumerateWeapons();
            switch (flag){
                case "All":
                    foreach(ThingWithComps w in pawnWeapons)
                    {
                        yield return new FloatMenuOption(w.def.label.Translate(), delegate
                        {
                            if (w.def.IsMeleeWeapon)
                                tracker.MeleeWeapon = w;
                            if (w.def.IsRangedWeapon)
                                tracker.RangedWeapon = w;
                        });
                    }
                    break;
                case "Melee":
                    List<ThingWithComps> m_MeleeWeapons = new List<ThingWithComps>();
                    foreach(ThingWithComps w in pawnWeapons)
                    {
                        if (w.def.IsMeleeWeapon)
                            yield return new FloatMenuOption(w.def.label.Translate(), delegate
                            {
                                tracker.MeleeWeapon = w;
                            });
                    }
                    break;
                case "Ranged":
                    List<ThingWithComps> m_RangedWeapons = new List<ThingWithComps>();
                    foreach(ThingWithComps w in pawnWeapons)
                    {
                        if (w.def.IsRangedWeapon)
                            yield return new FloatMenuOption(w.def.label.Translate(), delegate
                            {
                                tracker.RangedWeapon = w;
                            });
                    }
                    break;
            }
            yield return new FloatMenuOption("SC_CACW_None".Translate(), delegate
            {
                switch (flag)
                {
                    case "All":
                        tracker.MeleeWeapon = null;
                        tracker.RangedWeapon = null;
                        break;
                    case "Melee":
                        tracker.MeleeWeapon = null;
                        break;
                    case "Ranged":
                        tracker.RangedWeapon = null;
                        break;
                }
            });
        }

        public Command_AutoChangeWeapeon(Pawn pawn, Texture texture, Color color, bool flag)
        {
            PawnComp_SmartCombat pawnComp_SmartCombat = pawn.GetComp<PawnComp_SmartCombat>();
            this.pawn = pawn;
            defaultLabel = "SC_CACW_L".Translate();
            defaultDesc = "SC_CACW_D".Translate();
            if(pawnComp_SmartCombat.tracker.MeleeWeapon != null || pawnComp_SmartCombat.tracker.RangedWeapon != null)
            {
                defaultDesc += "SC_CACW_Dp".Translate();
                if (pawnComp_SmartCombat.tracker.MeleeWeapon != null)
                    defaultDesc += "SC_CACW_Dp_M".Translate(pawnComp_SmartCombat.tracker.MeleeWeapon.def.label);
                if (pawnComp_SmartCombat.tracker.RangedWeapon != null)
                    defaultDesc += "SC_CACW_Dp_R".Translate(pawnComp_SmartCombat.tracker.RangedWeapon.def.label);
            }
            if (texture == null) {
                icon = TexCommand.AttackMelee;
            }
            else
            {
                icon = texture;
            }
            if (flag)
            {
                color = color.SaturationChanged(0f);
                color = color.ToTransparent(0.6f);
            }
            SetColorOverride(color);
            Order = -100f;
            action = delegate
            {
                pawnComp_SmartCombat.disable = !flag;
            };
        }
    }

    [DefOf]
    public static class JobDefOf
    {
        public static JobDef Wait_SmartCombat;
    }

    public class JobGiver_SmartOrders : JobGiver_Orders
    {
        private Job PickupWeaponJob(Pawn pawn, Thing weapon, bool ignoreForbidden)
        {
            PawnComp_SmartCombat pawnComp_SmartCombat = pawn.GetComp<PawnComp_SmartCombat>();
            pawnComp_SmartCombat.tracker.DropWeapons.Remove(weapon);
            if (!pawn.CanReserveAndReach(weapon, PathEndMode.Touch, Danger.Deadly))
            {
                if (weapon.def.IsMeleeWeapon)
                {
                    pawnComp_SmartCombat.tracker.MeleeWeapon = null;
                }
                else
                {
                    pawnComp_SmartCombat.tracker.RangedWeapon = null;
                }
                return null;
            }
            if (weapon.IsBurning())
            {
                if (weapon.def.IsMeleeWeapon)
                {
                    pawnComp_SmartCombat.tracker.MeleeWeapon = null;
                }
                else
                {
                    pawnComp_SmartCombat.tracker.RangedWeapon = null;
                }
                return null;
            }
            Job job = JobMaker.MakeJob(RimWorld.JobDefOf.Equip, weapon);
            job.ignoreForbidden = ignoreForbidden;
            return job;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Drafted)
            {
                return JobMaker.MakeJob(JobDefOf.Wait_SmartCombat, pawn.Position);
            }
            else
            {
                PawnComp_SmartCombat pawnComp_SmartCombat = pawn.GetComp<PawnComp_SmartCombat>();
                if (pawnComp_SmartCombat != null && pawn.jobs.curJob == null && !pawn.GetPosture().Laying())
                {
                    Pawn_SmartCombatTracker tracker = pawnComp_SmartCombat.tracker;
                    if (!pawnComp_SmartCombat.disable && tracker.DropWeapons.Count() != 0)
                    {
                        return PickupWeaponJob(pawn, tracker.DropWeapons[0], true);
                    }
                }
            }
            return null;
        }
    }

    public class JobDriver_SmartWait : JobDriver_Wait
    {
        public PawnComp_SmartCombat pawnComp_SmartCombat;

        private void EnsureComponentAndTracker()
        {
            pawnComp_SmartCombat = pawn.GetComp<PawnComp_SmartCombat>();
            if(pawnComp_SmartCombat == null)
            {
                pawnComp_SmartCombat = new PawnComp_SmartCombat();
                pawn.AllComps.Add(pawnComp_SmartCombat);
                pawnComp_SmartCombat.pawn = pawn;
            }
            if(pawnComp_SmartCombat.tracker == null)
            {
                Pawn_SmartCombatTracker tracker = new Pawn_SmartCombatTracker
                {
                    pawn = pawn
                };
                pawnComp_SmartCombat.tracker = tracker;
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil toil = (job.forceSleep ? Toils_LayDown.LayDown(TargetIndex.A, hasBed: false, lookForOtherJobs: false) : ToilMaker.MakeToil("MakeNewToils"));
            toil.initAction = (Action)Delegate.Combine(toil.initAction, (Action)delegate
            {
                base.Map.pawnDestinationReservationManager.Reserve(pawn, job, pawn.Position);
                pawn.pather?.StopDead();
                CheckForAutoAttack();
            });
            toil.tickIntervalAction = (Action<int>)Delegate.Combine(toil.tickIntervalAction, (Action<int>)delegate (int delta)
            {
                if (job.expiryInterval == -1 && job.def == JobDefOf.Wait_SmartCombat && !pawn.Drafted)
                {
                    Log.Error(pawn?.ToString() + " in eternal WaitCombat without being drafted.");
                    ReadyForNextToil();
                }
                else
                {
                    if (job.forceSleep)
                    {
                        asleep = true;
                    }
                    if (GenTicks.IsTickIntervalDelta(pawn.thingIDNumber, 4, delta))
                    {
                        CheckForAutoAttack();
                    }
                }
            });
            DecorateWaitToil(toil);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            if (job.overrideFacing != Rot4.Invalid)
            {
                toil.handlingFacing = true;
                toil.tickAction = (Action)Delegate.Combine(toil.tickAction, (Action)delegate
                {
                    pawn.rotationTracker.FaceTarget(pawn.Position + job.overrideFacing.FacingCell);
                });
            }
            else if (pawn.mindState != null && pawn.mindState.duty != null && pawn.mindState.duty.focus != null && job.def != JobDefOf.Wait_SmartCombat)
            {
                LocalTargetInfo focusLocal = pawn.mindState.duty.focus;
                toil.handlingFacing = true;
                toil.tickAction = (Action)Delegate.Combine(toil.tickAction, (Action)delegate
                {
                    pawn.rotationTracker.FaceTarget(focusLocal);
                });
            }
            yield return toil;
        }

        public override void Notify_StanceChanged()
        {
            if(pawn.stances.curStance is Stance_Mobile)
            {
                CheckForAutoAttack();
            }
        }

        private void CheckForAutoAttack()
        {
            EnsureComponentAndTracker();
            if (!base.pawn.kindDef.canMeleeAttack || base.pawn.Downed || base.pawn.stances.FullBodyBusy || base.pawn.IsCarryingPawn() || (!base.pawn.IsPlayerControlled && base.pawn.IsPsychologicallyInvisible()) || base.pawn.IsShambler)
            {
                return;
            }
            collideWithPawns = false;
            bool flag = !base.pawn.WorkTagIsDisabled(WorkTags.Violent);
            bool flag2 = base.pawn.RaceProps.ToolUser && base.pawn.Faction == Faction.OfPlayer && !base.pawn.WorkTagIsDisabled(WorkTags.Firefighting);
            if (!(flag || flag2))
            {
                return;
            }
            Fire fire = null;
            for (int i = 0; i < 9; i++)
            {
                IntVec3 c = base.pawn.Position + GenAdj.AdjacentCellsAndInside[i];
                if (!c.InBounds(base.pawn.Map))
                {
                    continue;
                }
                List<Thing> thingList = c.GetThingList(base.Map);
                for (int j = 0; j < thingList.Count; j++)
                {
                    if (flag && base.pawn.kindDef.canMeleeAttack && thingList[j] is Pawn pawn && !pawn.ThreatDisabled(base.pawn) && base.pawn.HostileTo(pawn))
                    {
                        CompActivity comp = pawn.GetComp<CompActivity>();
                        if ((comp == null || comp.IsActive) && !base.pawn.ThreatDisabledBecauseNonAggressiveRoamer(pawn) && GenHostility.IsActiveThreatTo(pawn, base.pawn.Faction, ignoreHives: false))
                        {
                            pawnComp_SmartCombat.ChangeWeapon(base.pawn, "Melee");
                            base.pawn.meleeVerbs.TryMeleeAttack(pawn);
                            collideWithPawns = true;
                            pawnComp_SmartCombat.AC_flag = true;
                            return;
                        }
                    }
                    if (flag2 && thingList[j] is Fire fire2 && (fire == null || fire2.fireSize < fire.fireSize || i == 8) && (fire2.parent == null || fire2.parent != base.pawn))
                    {
                        fire = fire2;
                    }
                }
            }
            if (fire != null && (!base.pawn.InMentalState || base.pawn.MentalState.def.allowBeatfire))
            {
                base.pawn.natives.TryBeatFire(fire);
            }
            else
            {
                if (!flag || !job.canUseRangedWeapon || job.def != JobDefOf.Wait_SmartCombat || (base.pawn.drafter != null && !base.pawn.drafter.FireAtWill))
                {
                    return;
                }
                if(pawnComp_SmartCombat.AC_flag)
                    pawnComp_SmartCombat.ChangeWeapon(base.pawn, "Ranged");
                Verb currentEffectiveVerb = base.pawn.CurrentEffectiveVerb;
                if (currentEffectiveVerb != null && !currentEffectiveVerb.verbProps.IsMeleeAttack)
                {
                    TargetScanFlags targetScanFlags = TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
                    if (currentEffectiveVerb.IsIncendiary_Ranged())
                    {
                        targetScanFlags |= TargetScanFlags.NeedNonBurning;
                    }
                    Thing thing = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(base.pawn, targetScanFlags);
                    if (thing != null)
                    {
                        base.pawn.TryStartAttack(thing);
                        collideWithPawns = true;
                        pawnComp_SmartCombat.AC_flag = true;
                    }
                    else
                    {
                        pawnComp_SmartCombat.AC_flag = false;
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }
    }

    public class Pawn_SmartCombatTracker : IExposable
    {
        public Pawn pawn;
        
        public ThingWithComps RangedWeapon;

        public ThingWithComps MeleeWeapon;

        public List<Thing> DropWeapons = new List<Thing>();

        public void CheckWeapons()
        {
            if(RangedWeapon != null && !pawn.TryFindInInventory(RangedWeapon.def, inclEquipped: true, out var thing) && !DropWeapons.Contains(RangedWeapon))
            {
                DropWeapons.Add(RangedWeapon);
            }
            if(MeleeWeapon != null && !pawn.TryFindInInventory(MeleeWeapon.def, inclEquipped: true, out var thing2) && !DropWeapons.Contains(MeleeWeapon))
            {
                DropWeapons.Add(MeleeWeapon);
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "SC_tP");
            Scribe_References.Look(ref RangedWeapon, "SC_tRW");
            Scribe_References.Look(ref MeleeWeapon, "SC_tMW");
            Scribe_Collections.Look(ref DropWeapons, "SC_tDWs", LookMode.Reference);
        }
    }

    public class PawnComp_SmartCombat : ThingComp
    {
        public Pawn pawn;

        public Pawn_SmartCombatTracker tracker;

        public bool AC_flag = false;

        public bool disable = false;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            Texture texture = null;
            Color color = Color.white;
            (Texture, Color) tuple = (null, Color.white);
            if (pawn == null || pawn.Faction != Faction.OfPlayer || tracker == null || !pawn.Drafted)
            {
                yield break;
            }
            if(pawn.equipment.Primary == null)
            {
                if(tracker.RangedWeapon == null)
                {
                    if(tracker.MeleeWeapon != null)
                    {
                        tuple = GizmoHelper.ResolveIcon(tracker.MeleeWeapon);
                        texture = tuple.Item1;
                        color = tuple.Item2;
                        yield return new Command_AutoChangeWeapeon(pawn, texture: texture, color: color, disable);
                        yield break;
                    }
                    yield return new Command_AutoChangeWeapeon(pawn, texture, color, disable);
                    yield break;
                }
                else
                {
                    tuple = GizmoHelper.ResolveIcon(tracker.RangedWeapon);
                    texture = tuple.Item1;
                    color = tuple.Item2;
                    yield return new Command_AutoChangeWeapeon(pawn, texture: texture, color: color, disable);
                    yield break;
                }
            }
            else if (tracker.RangedWeapon != null && pawn.equipment.Primary.def.IsMeleeWeapon) {
                tuple = GizmoHelper.ResolveIcon(tracker.RangedWeapon);
                texture = tuple.Item1;
                color = tuple.Item2;
            }
            else if (tracker.MeleeWeapon != null && pawn.equipment.Primary.def.IsRangedWeapon) {
                tuple = GizmoHelper.ResolveIcon(tracker.MeleeWeapon);
                texture = tuple.Item1;
                color = tuple.Item2;
            }
            yield return new Command_AutoChangeWeapeon(pawn, texture: texture, color: color, disable);
        }

        public void ChangeWeapon(Pawn pawn, string flag)
        {
            if (pawn.equipment.Primary == null)
            {
                if (tracker.MeleeWeapon == null && tracker.RangedWeapon == null || disable)
                    return;
                switch (flag)
                {
                    case "Melee":
                        if (tracker.MeleeWeapon != null)
                            pawn.EquipFromInventory(tracker.MeleeWeapon);
                        else
                            return;
                        break;
                    case "Ranged":
                        if (tracker.RangedWeapon != null)
                            pawn.EquipFromInventory(tracker.RangedWeapon);
                        else
                            return;
                        break;
                }
                return;
            }
            switch (flag)
            {
                case "Melee":
                    if (tracker.MeleeWeapon != null && pawn.equipment.Primary.def.IsRangedWeapon && !disable)
                        pawn.EquipFromInventory(tracker.MeleeWeapon);
                    else
                        return;
                    break;
                case "Ranged":
                    if (tracker.RangedWeapon != null && pawn.equipment.Primary.def.IsMeleeWeapon && !disable)
                        pawn.EquipFromInventory(tracker.RangedWeapon);
                    else
                        return;
                    break;
            }
        }

        public override void CompTickInterval(int delta)
        {
            base.CompTickInterval(delta);
            tracker.CheckWeapons();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref AC_flag, "SC_PCSC_f");
            Scribe_References.Look(ref pawn, "SC_PCSC_p");
            Scribe_Deep.Look(ref tracker, "SC_PCSC_t");
        }

    }

    [HarmonyPatch(typeof(ITab_Pawn_Gear))]
    public class Patch_ITab_Pawn_Gear
    {
        [HarmonyPatch("InterfaceDrop", new Type[] {typeof(Thing) })]
        [HarmonyPostfix]
        public static void Postfix_InterfaceDrop(ITab_Pawn_Gear __instance, Thing t)
        {
            Traverse trav = Traverse.Create(__instance);
            Pawn pawn = trav.Method("get_SelPawnForGear", new Type[0]).GetValue<Pawn>();
            PawnComp_SmartCombat pawnComp_SmartCombat = pawn.GetComp<PawnComp_SmartCombat>();
            if (t.def.IsWeapon && pawnComp_SmartCombat != null)
            {
                if (pawnComp_SmartCombat.tracker.MeleeWeapon == t)
                    pawnComp_SmartCombat.tracker.MeleeWeapon = null;
                else if (pawnComp_SmartCombat.tracker.RangedWeapon == t)
                    pawnComp_SmartCombat.tracker.RangedWeapon = null;
            }
        }
    }
}
