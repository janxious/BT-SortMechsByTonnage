using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BattleTech;
using BattleTech.UI;
using Harmony;
using Newtonsoft.Json;
using UnityEngine;
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

        internal static float CalculateCBillValue(MechDef mech)
        {
            var currentCBillValue = 0f;
            var num = 10000f;
            currentCBillValue = (float) mech.Chassis.Description.Cost;
            var num2 = 0f;
            num2 += mech.Head.CurrentArmor;
            num2 += mech.CenterTorso.CurrentArmor;
            num2 += mech.CenterTorso.CurrentRearArmor;
            num2 += mech.LeftTorso.CurrentArmor;
            num2 += mech.LeftTorso.CurrentRearArmor;
            num2 += mech.RightTorso.CurrentArmor;
            num2 += mech.RightTorso.CurrentRearArmor;
            num2 += mech.LeftArm.CurrentArmor;
            num2 += mech.RightArm.CurrentArmor;
            num2 += mech.LeftLeg.CurrentArmor;
            num2 += mech.RightLeg.CurrentArmor;
            num2 *= UnityGameInstance.BattleTechGame.MechStatisticsConstants.CBILLS_PER_ARMOR_POINT;
            currentCBillValue += num2;
            currentCBillValue += mech.Inventory.Sum(mechComponentRef => (float) mechComponentRef.Def.Description.Cost);
            currentCBillValue = Mathf.Round(currentCBillValue / num) * num;
            return currentCBillValue;
        }

        internal static List<Tuple<MechState, MechDef>> SortMechDefs(Dictionary<int, Tuple<MechState, MechDef>> mechs)
        {
            if (ModSettings.OrderByNickname)
            {
                return
                    mechs
                        .Values
                        .OrderBy(mech => mech.Item2.Name)
                        .ThenBy(mech => mech.Item2.Chassis.Tonnage)
                        .ThenBy(mech => mech.Item2.Chassis.VariantName).ToList();
            }

            if (ModSettings.OrderByCbillValue)
            {
                return
                    mechs
                        .Values
                        .OrderByDescending(mech => CalculateCBillValue(mech.Item2))
                        .ThenBy(mech => mech.Item2.Chassis.Tonnage)
                        .ThenBy(mech => mech.Item2.Chassis.VariantName).ToList();
            }

            return
                mechs
                    .Values
                    .OrderByDescending(mech => mech.Item2.Chassis.Tonnage)
                    .ThenBy(mech => mech.Item2.Chassis.VariantName).ToList();
        }

        public static Dictionary<int, Tuple<MechState, MechDef>> CombinedSlots(int mechSlots,
            Dictionary<int, MechDef> active, Dictionary<int, MechDef> readying)
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
                    combined[i] = new Tuple<MechState, MechDef>(MechState.Readying, readying[i]);
                }
            }

            Logger.Debug($"combined size: {combined.Count}");
            return combined;
        }

        public static void SortMechsByTonnage(int mechSlots, Dictionary<int, MechDef> activeMechs,
            Dictionary<int, MechDef> readyingMechs)
        {
            var sortedMechs = SortMechDefs(CombinedSlots(mechSlots, activeMechs, readyingMechs));
            Logger.Debug($"sortedMechs #: {sortedMechs.Count}");
            for (var ii = 0; ii < sortedMechs.Count; ii++)
            {
                Logger.Debug($"mech: {sortedMechs[ii].Item2.Name} / {sortedMechs[ii].Item2.Chassis.VariantName}\nreadying? {sortedMechs[ii].Item1 == MechState.Readying}\nactive? {sortedMechs[ii].Item1 == MechState.Active}");
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
        static bool Prefix(int idx, MechDef mech, bool active, bool forcePlacement, bool displayMechPopup,
            string mechAddedHeader, SimGameState __instance)
        {
            Logger.Debug("AddMech Prefix Patch Installed");
            if (displayMechPopup)
            {
                if (string.IsNullOrEmpty(mech.GUID))
                {
                    mech.SetGuid(__instance.GenerateSimGameUID());
                }

                var companyStats = Traverse.Create(__instance).Field("companyStats").GetValue<StatCollection>();
                companyStats.ModifyStat<int>("Mission", 0, "COMPANY_MechsAdded", StatCollection.StatOperation.Int_Add, 1, -1, true);
                if (string.IsNullOrEmpty(mechAddedHeader))
                {
                    mechAddedHeader = "'Mech Chassis Complete";
                    int num = (int) WwiseManager.PostEvent<AudioEventList_ui>(AudioEventList_ui.ui_sim_popup_newChassis, WwiseManager.GlobalAudioObject, (AkCallbackManager.EventCallback) null, (object) null);
                }

                mechAddedHeader += ": {0}";

                __instance.GetInterruptQueue().QueuePauseNotification(
                    string.Format(mechAddedHeader, (object) mech.Description.UIName), mech.Chassis.YangsThoughts,
                    __instance.GetCrewPortrait(SimGameCrew.Crew_Yang), "notification_mechreadycomplete", (Action) (() =>
                    {
                        int firstFreeMechBay = __instance.GetFirstFreeMechBay();
                        if (firstFreeMechBay >= 0)
                        {
                            __instance.ActiveMechs[firstFreeMechBay] = mech;
                            SortMechsByTonnage(__instance.GetMaxActiveMechs(), __instance.ActiveMechs,
                                __instance.ReadyingMechs);
                        }
                        else
                            __instance.CreateMechPlacementPopup(mech);
                    }), "Continue", (Action) null, (string) null);
                return false;
            }

            return true;
        }

        static void Postfix(int idx, MechDef mech, bool active, bool forcePlacement, bool displayMechPopup,
            string mechAddedHeader, SimGameState __instance)
        {
            Logger.Debug("AddMech Postfix Patch Installed");
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

    [HarmonyPatch(typeof(SimGameState), "RemoveMech")]
    public static class SimGameState_RemoveMech_Patch
    {
        static void Postfix(int idx, MechDef mech, bool active, SimGameState __instance)
        {
            Logger.Debug("RemoveMech Patch Installed");
            SortMechsByTonnage(__instance.GetMaxActiveMechs(), __instance.ActiveMechs, __instance.ReadyingMechs);
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ScrapActiveMech")]
    public static class SimGameState_ScrapActiveMech_Patch
    {
        static void Postfix(int baySlot, MechDef def, SimGameState __instance)
        {
            Logger.Debug("ScrapActiveMech Patch Installed");
            SortMechsByTonnage(__instance.GetMaxActiveMechs(), __instance.ActiveMechs, __instance.ReadyingMechs);
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ScrapInactiveMech")]
    public static class SimGameState_ScrapInativeMech_Patch
    {
        static void Postfix(string id, bool pay, SimGameState __instance)
        {
            Logger.Debug("ScrapInctiveMech Patch Installed");
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

    [HarmonyPatch(typeof(SimGameState), "UnreadyMech")]
    public static class SimGameState_UnreadyMech_Patch
    {
        static void Postfix(int baySlot, MechDef def, SimGameState __instance)
        {
            Logger.Debug("UnreadyMech Patch Installed");
            SortMechsByTonnage(__instance.GetMaxActiveMechs(), __instance.ActiveMechs, __instance.ReadyingMechs);
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ML_ReadyMech")]
    public static class SimGamestate_ML_ReadyMech_Patch
    {
        static bool Prefix(WorkOrderEntry_ReadyMech order, SimGameState __instance)
        {
            Logger.Debug("ML_ReadyMech Patch Installed");
            if (order.IsMechLabComplete)
            {
                return false;
            }

            var index = __instance.ReadyingMechs.First(item => item.Value == order.Mech).Key;
            __instance.ReadyingMechs.Remove(index);
            __instance.ActiveMechs[index] = order.Mech;
            order.Mech.RefreshBattleValue();
            order.SetMechLabComplete(true);
            SortMechsByTonnage(__instance.GetMaxActiveMechs(), __instance.ActiveMechs, __instance.ReadyingMechs);
            return false;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "Cancel_ML_ReadyMech", new Type[] {typeof(WorkOrderEntry_ReadyMech)})]
    public static class SimGamestate_Cancel_ML_ReadyMech_Patch
    {
        static bool Prefix(WorkOrderEntry_ReadyMech order, SimGameState __instance)
        {
            Logger.Debug("ML_Cancel_ReadyMech Patch Installed");
            if (order.IsMechLabComplete)
            {
                Logger.Debug("wtf is happening how");
                return false;
            }

            var item = __instance.ReadyingMechs.First(readying => readying.Value == order.Mech);
            var index = item.Key;
            Logger.Debug($"cancel index: {index}\nmech? {item.Value.GUID} : {order.Mech.GUID} : {order.Mech.Chassis.VariantName}");
            __instance.UnreadyMech(index, order.Mech);
            __instance.ReadyingMechs.Remove(index);
            SortMechsByTonnage(__instance.GetMaxActiveMechs(), __instance.ActiveMechs, __instance.ReadyingMechs);
            return false;
        }
    }

    [HarmonyPatch(typeof(MechBayPanel), "OnMechLabClosed")]
    public static class COMEON
    {
        static void Prefix(MechBayPanel __instance)
        {
            Logger.Debug("OnMechLabClosed Patch Installed");
            if (!__instance.IsSimGame)
            {
                Logger.Debug("wut");
                return;
            }
            SortMechsByTonnage(__instance.Sim.GetMaxActiveMechs(), __instance.Sim.ActiveMechs, __instance.Sim.ReadyingMechs);
        }
    }
}