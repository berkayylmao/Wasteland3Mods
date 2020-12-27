// clang-format off
// 
//    BetterEquipmentMods (Wasteland3Mods)
//    Copyright (C) 2020 Berkay Yigit <berkaytgy@gmail.com>
// 
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU Affero General Public License as published
//    by the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
// 
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU Affero General Public License for more details.
// 
//    You should have received a copy of the GNU Affero General Public License
//    along with this program. If not, see <https://www.gnu.org/licenses/>.
// 
// clang-format on

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using BepInEx;
using BepInEx.IL2CPP;

using HarmonyLib;

using InXile.Inventory;
using InXile.UI.Extensions;
using InXile.UI.WL3;

using JetBrains.Annotations;

namespace BetterEquipmentMods {
   [BepInProcess("WL3.exe")]
   [BepInPlugin(GUID: "berkayylmao.BetterEquipmentMods", Name: "Better Equipment Mods", Version: "1.0.0.0")]
   [SuppressMessage(category: "ReSharper", checkId: "ClassNeverInstantiated.Global")]
   public class Main : BasePlugin {
      [SuppressMessage(category: "ReSharper", checkId: "InconsistentNaming")]
      private static class Patches {
         [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
         internal static class ItemContextWindowPatches {
            internal static void Prefix_OnYesFieldStripClicked(ItemContextWindow __instance) {
               if (__instance.itemInstance.TryCast<ItemInstance_Equipment>() is var equipment) {
                  foreach (var slot in equipment.AllowedModSlots) {
                     if (!equipment.HasModInSlot(slot)) continue;

                     __instance.ownerInventory.AsyncAdd(equipment.GetMod(slot), count: 1, onCompleteCallback: null);
                     equipment.RemoveMod(slot);
                  }
               }
            }
         }

         [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
         internal static class MainMenuWindowPatches {
            internal static void Postfix_Awake(MainMenuWindow __instance) {
               var _text = UnityEngine.Object.Instantiate(__instance.versionLabel,
                                                          __instance.versionLabel.transform.position,
                                                          __instance.versionLabel.transform.rotation,
                                                          __instance.versionLabel.transform.parent);

               _text.text = "(BetterEquipmentMods v1.0.0)";
            }
         }

         [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
         internal static class ModEquipmentWindowPatches {
            internal static Boolean Prefix_HasAppliedMods(ref Boolean __result) {
               __result = true;

               return false;
            }

            internal static void Postfix_Init(ModEquipmentWindow __instance) {
               foreach (var btn in __instance.canvas.GetComponentsInChildren<InXile.UI.MultiTargetButton>()) {
                  switch (btn.name) {
                     case "Cancel Button":
                        btn.onClick.RemoveAllListeners();
                        UnityEngine.Object.Destroy(btn.gameObject);

                        break;
                     case "Confirm Button": {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(new Action(() => __instance.Hide(wasCancelled: false)));

                        btn.interactable = true;

                        var _dbTextSource = btn.GetComponentInChildren<StringDBTextSource>();
                        var _dbString     = _dbTextSource.SourceString;
                        _dbString.StringTable = DatabaseString.StringTableType.Gui;
                        _dbString.StringID    = StringDefines.Gui.CLOSE;

                        _dbTextSource.NotifyBecameDirty();

                        break;
                     }
                  }
               }
            }

            internal static void Prefix_OnItemInteractClicked(ModEquipmentWindow __instance,
                                                              InventoryItem      item) {
               var _newModTemplate = item.Instance.ItemTemplate.Cast<ItemTemplate_Mod>();

               if (__instance.equipmentInstance.HasModInSlot(_newModTemplate.slot)) {
                  __instance.selectedCharacter.Inventory.AsyncAdd(__instance.equipmentInstance.GetMod(_newModTemplate.slot),
                                                                  count: 1,
                                                                  new Action<Boolean>((wasAdded) =>
                                                                                         __instance.inventoryPanel.RebuildInventoryButtons()));
                  __instance.equipmentInstance.RemoveMod(_newModTemplate.slot);
               }

               if (__instance.dummyItemInstance.HasModInSlot(_newModTemplate.slot)) __instance.dummyItemInstance.RemoveMod(_newModTemplate.slot);
            }

            internal static void Postfix_PopulateData(ModEquipmentWindow __instance) {
               foreach (var _loop_dict in __instance.itemPanel.modDisplayPanel.slotDisplays) {
                  var _modSlotDisplay = _loop_dict.Value;

                  _modSlotDisplay.SetDoubleClickCallback(callback: new Action<Int32>((i) => {
                     var _slotId = (ItemTemplate_Mod.ModSlot)i;

                     Boolean _dummyEquipmentHasMod  = __instance.dummyItemInstance.HasModInSlot(_slotId);
                     Boolean _actualEquipmentHasMod = __instance.equipmentInstance.HasModInSlot(_slotId);

                     // dummy is empty, ignore input
                     if (!_dummyEquipmentHasMod) return;

                     if (_actualEquipmentHasMod) {
                        __instance.selectedCharacter.Inventory.AsyncAdd(_modSlotDisplay.inventoryItem,
                                                                        new Action<Boolean>((wasAdded) => __instance.inventoryPanel
                                                                                               .RebuildInventoryButtons()));
                        __instance.equipmentInstance.RemoveMod(_slotId);
                     }

                     __instance.DoUnapplyModFromDummyItem(item: _modSlotDisplay.InventoryItem);
                  }));
               }
            }
         }
      }

      public override void Load() {
         this.Log.LogDebug("In memory.");
         this.Log.LogInfo("Implementing patches.");

         var _harmony = new Harmony(id: "berkayylmao.BetterEquipmentMods");
         // ItemContextWindow
         _harmony.Patch(AccessTools.Method(typeof(ItemContextWindow), name: "OnYesFieldStripClicked"),
                        new HarmonyMethod(method: AccessTools.Method(typeof(Patches.ItemContextWindowPatches),
                                                                     name: "Prefix_OnYesFieldStripClicked")));
         // ModEquipmentWindow
         _harmony.Patch(AccessTools.Method(typeof(ModEquipmentWindow), name: "HasAppliedMods"),
                        new HarmonyMethod(method: AccessTools.Method(typeof(Patches.ModEquipmentWindowPatches), name: "Prefix_HasAppliedMods")));
         _harmony.Patch(AccessTools.Method(typeof(ModEquipmentWindow), name: "Init"),
                        prefix: null,
                        new HarmonyMethod(method: AccessTools.Method(typeof(Patches.ModEquipmentWindowPatches), name: "Postfix_Init")));
         _harmony.Patch(AccessTools.Method(typeof(ModEquipmentWindow), name: "OnItemInteractClicked"),
                        new HarmonyMethod(method: AccessTools.Method(typeof(Patches.ModEquipmentWindowPatches),
                                                                     name: "Prefix_OnItemInteractClicked")));
         _harmony.Patch(AccessTools.Method(typeof(ModEquipmentWindow), name: "PopulateData"),
                        prefix: null,
                        new HarmonyMethod(method: AccessTools.Method(typeof(Patches.ModEquipmentWindowPatches), name: "Postfix_PopulateData")));
         // MainMenuWindow
         _harmony.Patch(AccessTools.Method(typeof(MainMenuWindow), name: "Awake"),
                        prefix: null,
                        new HarmonyMethod(method: AccessTools.Method(typeof(Patches.MainMenuWindowPatches), name: "Postfix_Awake")));

         this.Log.LogInfo("Patches are successfully installed.");
         this.Log.LogMessage($"Version {Assembly.GetExecutingAssembly().GetName().Version} is successfully loaded.");
      }
   }
}
