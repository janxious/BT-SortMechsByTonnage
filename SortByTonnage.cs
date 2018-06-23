using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BattleTech;
using Harmony;
using Newtonsoft.Json;
using static SortByTonnage.SortByTonnage;

namespace SortByTonnage
{
    public static class SortByTonnage
    {
        internal static Settings ModSettings = new Settings();
        internal static string ModDirectory;

        public static void Init(string directory, string settingsJSON)
        {
            ModDirectory = directory;
            try
            {
                ModSettings = JsonConvert.DeserializeObject<Settings>(settingsJSON);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                ModSettings = new Settings();
            }

            var harmony = HarmonyInstance.Create("com.joelmeador.SortByTonnage");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public enum MechState
        {
            Active,
            Readying,
        }

        internal static List<Tuple<MechState, MechDef>> SortMechDefs(Dictionary<int, Tuple<MechState, MechDef>> mechs)
        {
            return 
                mechs
                    .Values
                    .OrderByDescending(mech => mech.Item2.Chassis.Tonnage)
                    .ThenBy(mech => mech.Item2.Chassis.VariantName).ToList();
        }

        public static Dictionary<int, Tuple<MechState, MechDef>> CombinedSlots(int mechSlots, Dictionary<int, MechDef> active, Dictionary<int, MechDef> readying)
        {
            var combined = new Dictionary<int, Tuple<MechState, MechDef>>();

            for (var i = 0; i <= mechSlots; i++)
            {
                if (active.ContainsKey(i))
                {
                    Logger.Debug($"found active {i}");
                    combined[i] = new Tuple<MechState, MechDef>(MechState.Active, active[i]);
                }
                else if (readying.ContainsKey(i))
                {
                    Logger.Debug($"found readying {i}");
                    combined[i] = new Tuple<MechState, MechDef>(MechState.Active, readying[i]);;
                }
            }
            Logger.Debug($"combined size: {combined.Count}");
            return combined;
        }

        public static void SortMechsByTonnage(int mechSlots, Dictionary<int, MechDef> activeMechs, Dictionary<int, MechDef> readyingMechs)
        {
            var sortedMechs = SortMechDefs(CombinedSlots(mechSlots, activeMechs, readyingMechs));
            Logger.Debug($"sortedMechswtf: {sortedMechs.Count}");
            for (var ii = 0; ii < sortedMechs.Count; ii++)
            {
                Logger.Debug($"mech: {sortedMechs[ii].Item2.Chassis.VariantName}\nreadying? {sortedMechs[ii].Item1 == MechState.Readying}\nactive? {sortedMechs[ii].Item1 == MechState.Active}");
            }
            for (var i = 0; i <= mechSlots; i++)
            {
                if (i < sortedMechs.Count)
                {
                    
                    if (activeMechs.ContainsKey(i))
                    {
                        activeMechs.Remove(i);
                    } 
                    else if (readyingMechs.ContainsKey(i))
                    {
                        readyingMechs.Remove(i);
                    }

                        if (sortedMechs[i].Item1 == MechState.Active)
                    {
                        activeMechs.Add(i, sortedMechs[i].Item2);
                    }
                    else if (sortedMechs[i].Item1 == MechState.Readying)
                    {
                        readyingMechs.Add(i, sortedMechs[i].Item2);
                    }
                }
                else
                {
                    if (activeMechs.ContainsKey(i))
                    {
                        activeMechs.Remove(i);
                    }
                    else if (readyingMechs.ContainsKey(i))
                    {
                        readyingMechs.Remove(i);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(SimGameState), "AddMech")]
    public static class SimGameState_AddMech_Patch
    {
        static void Postfix(int idx, MechDef mech, bool active, bool forcePlacement, bool displayMechPopup,
            string mechAddedHeader, SimGameState __instance)
        {
            Logger.Debug("AddMech Patch Installed");
            SortMechsByTonnage(__instance.GetMaxActiveMechs(), __instance.ActiveMechs, __instance.ReadyingMechs);
        }
    }

    [HarmonyPatch(typeof(SimGameState), "RemoveMech")]
    public static class SimGameState_RemoveMech_Patch
    {
        static void Postfix(int idx, MechDef mech, bool active, SimGameState __instance)
        {
            Logger.Debug("RemoveMech Patch Installed");
            SortMechsByTonnage(__instance.GetMaxActiveMechs(), __instance.ActiveMechs, __instance.ReadyingMechs);
        }
    }


    [HarmonyPatch(typeof(SimGameState), "StripMech")]
    public static class SimGameState_StripMech_Patch
    {
        static void Postfix(int baySlot, MechDef def, SimGameState __instance)
        {
            Logger.Debug("StripMech Patch Installed");
            SortMechsByTonnage(__instance.GetMaxActiveMechs(), __instance.ActiveMechs, __instance.ReadyingMechs);
        }
    }
    
    [HarmonyPatch(typeof(SimGameState), "ReadyMech")]
    public static class SimGameState_ReadyMech_Patch
    {
        static void Postfix(int baySlot, string id, SimGameState __instance)
        {
            Logger.Debug("ReadyMech Patch Installed");
            SortMechsByTonnage(__instance.GetMaxActiveMechs(), __instance.ActiveMechs, __instance.ReadyingMechs);
        }
    }
}