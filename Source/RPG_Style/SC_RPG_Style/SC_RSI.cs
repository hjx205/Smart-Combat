using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using HarmonyLib;
using Sandy_Detailed_RPG_Inventory;
using Hjx_SmartCombat;

namespace SC_RPG_Style
{
    [StaticConstructorOnStartup]
    public class StartUp
    {
        static StartUp()
        {
            Log.Message("Smart Combat: RPG Style Inventory v1.0");
            var harmony = new Harmony("Hjx.SmartCombat.RSI");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Sandy_Detailed_RPG_GearTab))]
    public class Patch_ITab_Pawn_Gear
    {
        [HarmonyPatch("InterfaceDrop", new Type[] { typeof(Thing) })]
        [HarmonyPostfix]
        public static void Postfix_InterfaceDrop(Sandy_Detailed_RPG_GearTab __instance, Thing t)
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
