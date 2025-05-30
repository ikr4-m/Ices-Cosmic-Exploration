﻿using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using ECommons;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices.Legacy;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.Logging;
using ECommons.Reflection;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.WKS;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ICE.Enums;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;

namespace ICE.Utilities;

public static unsafe class Utils
{
    #region Plugin/Ecoms stuff

    public static bool HasPlugin(string name) => DalamudReflector.TryGetDalamudPlugin(name, out _, false, true);
    internal static bool GenericThrottle => FrameThrottler.Throttle("AutoRetainerGenericThrottle", 10);
    internal static bool LogThrottle => FrameThrottler.Throttle("AutoRetainerGenericThrottle", 2000);
    public static TaskManagerConfiguration DConfig => new(timeLimitMS: 10 * 60 * 3000, abortOnTimeout: false);

    public static void PluginVerbos(string message) => PluginLog.Verbose(message);
    public static void PluginInfo(string message) => PluginLog.Information(message);

    public static void PluginDebug(string message)
    {
        if (EzThrottler.Throttle(message, 1000))
            PluginLog.Debug(message);
    }

    public static void PluginWarning(string message)
    {
        if (EzThrottler.Throttle(message, 1000))
            PluginLog.Warning(message);
    }

    public static void OpenStellaMission()
    {
        if (TryGetAddonMaster<WKSHud>("WKSHud", out var hud) && hud.IsAddonReady && !IsAddonActive("WKSMissionInfomation"))
        {
            if (EzThrottler.Throttle("Opening Steller Missions"))
            {
                PluginLog.Debug("Opening Mission Menu");
                hud.Mission();
            }
        }
    }

    public static unsafe void SetFlagForNPC(uint territoryId, float x, float y)
    {
        var terSheet = Svc.Data.GetExcelSheet<TerritoryType>();
        var map = terSheet.GetRow(territoryId).Map.Value;

        var agent = AgentMap.Instance();

        Vector2 pos = MapToWorld(new Vector2(x, y), map.SizeFactor, map.OffsetX, map.OffsetY);

        agent->IsFlagMarkerSet = false;
        agent->SetFlagMapMarker(territoryId, map.RowId, pos.X, pos.Y);
        agent->OpenMapByMapId(map.RowId, territoryId);
    }

    public static float MapToWorld(float value, uint scale, int offset) => -offset * (scale / 100.0f) + 50.0f * (value - 1) * (scale / 100.0f);

    public static Vector2 MapToWorld(Vector2 coordinates, ushort sizeFactor, short offsetX, short offsetY)
    {
        var scalar = sizeFactor / 100.0f;

        var xWorldCoord = MapToWorld(coordinates.X, sizeFactor, offsetX);
        var yWorldCoord = MapToWorld(coordinates.Y, sizeFactor, offsetY);

        var objectPosition = new Vector2(xWorldCoord, yWorldCoord);
        var center = new Vector2(1024.0f, 1024.0f);

        return objectPosition / scalar - center / scalar;
    }

    #endregion

    #region Player Information

    public static uint? GetClassJobId() => Svc.ClientState.LocalPlayer?.ClassJob.RowId;
    public static bool IsPlayerReady() => Svc.ClientState.LocalPlayer != null;
    public static bool UsingSupportedJob() => GetClassJobId() >= 8 && GetClassJobId() <= 18;
    public static unsafe int GetLevel(int expArrayIndex = -1)
    {
        if (expArrayIndex == -1) expArrayIndex = Svc.ClientState.LocalPlayer?.ClassJob.Value.ExpArrayIndex ?? 0;
        return UIState.Instance()->PlayerState.ClassJobLevels[expArrayIndex];
    }
    internal static unsafe short GetCurrentLevelFromSheet(Job? job = null)
    {
        PlayerState* playerState = PlayerState.Instance();
        return playerState->ClassJobLevels[Svc.Data.GetExcelSheet<ClassJob>().GetRowOrDefault((uint)(job ?? (Player.Available ? Player.Object.GetJob() : 0)))?.ExpArrayIndex ?? 0];
    }

    public static bool IsInCosmicZone() => IsInZone(1237);
    public static bool IsInSinusArdorum() => IsInZone(1237);
    public static bool IsInZone(uint zoneID) => Svc.ClientState.TerritoryType == zoneID;
    public static uint CurrentTerritory() => GameMain.Instance()->CurrentTerritoryTypeId;

    public static bool IsBetweenAreas => Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BetweenAreas51];

    public static bool PlayerNotBusy()
    {
        return Player.Available
               && Player.Object.CastActionId == 0
               && !IsOccupied()
               && !Player.IsJumping
               && Player.Object.IsTargetable
               && !Player.IsAnimationLocked;
    }

    public static unsafe bool HasStatusId(params uint[] statusIDs)
    {
        var statusID = Svc.ClientState.LocalPlayer?.StatusList
            .Select(se => se.StatusId)
            .ToList().Intersect(statusIDs)
            .FirstOrDefault();

        return statusID != default;
    }

    public static int GetGp()
    {
        var gp = Svc.ClientState.LocalPlayer?.CurrentGp ?? 0;
        return (int)gp;
    }

    internal static unsafe float GetDistanceToPlayer(Vector3 v3) => Vector3.Distance(v3, Player.GameObject->Position);
    internal static unsafe float GetDistanceToPlayer(IGameObject gameObject) => GetDistanceToPlayer(gameObject.Position);

    public static unsafe int GetItemCount(int itemID, bool includeHq = true)
    => includeHq ? InventoryManager.Instance()->GetInventoryItemCount((uint)itemID, true)
    + InventoryManager.Instance()->GetInventoryItemCount((uint)itemID) + InventoryManager.Instance()->GetInventoryItemCount((uint)itemID + 500_000)
    : InventoryManager.Instance()->GetInventoryItemCount((uint)itemID) + InventoryManager.Instance()->GetInventoryItemCount((uint)itemID + 500_000);

    public static Vector3 NavDestination = Vector3.Zero;

    public static unsafe uint CurrentLunarMission => WKSManager.Instance()->CurrentMissionUnitRowId;

    #endregion

    #region Target Information

    internal static bool? TargetgameObject(IGameObject? gameObject)
    {
        var x = gameObject;
        if (Svc.Targets.Target != null && Svc.Targets.Target.DataId == x.DataId)
            return true;

        if (!IsOccupied())
        {
            if (x != null)
            {
                if (EzThrottler.Throttle($"Throttle Targeting {x.DataId}"))
                {
                    Svc.Targets.SetTarget(x);
                    ECommons.Logging.PluginLog.Information($"Setting the target to {x.DataId}");
                }
            }
        }
        return false;
    }
    internal static bool TryGetObjectByDataId(ulong dataId, out IGameObject? gameObject) => (gameObject = Svc.Objects.OrderBy(GetDistanceToPlayer).FirstOrDefault(x => x.DataId == dataId)) != null;
    internal static unsafe void InteractWithObject(IGameObject? gameObject)
    {
        try
        {
            if (gameObject == null || !gameObject.IsTargetable)
                return;
            var gameObjectPointer = (GameObject*)gameObject.Address;
            TargetSystem.Instance()->InteractWithObject(gameObjectPointer, false);
        }
        catch (Exception ex)
        {
            Svc.Log.Info($"InteractWithObject: Exception: {ex}");
        }
    }

    #endregion

    #region Addon Information

    public static bool IsAddonActive(string AddonName) // Used to see if the addon is active/ready to be fired on
    {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonByName(AddonName);
        return addon != null && addon->IsVisible && addon->IsReady;
    }

    public static unsafe bool IsNodeVisible(string addonName, params int[] ids)
    {
        var ptr = Svc.GameGui.GetAddonByName(addonName, 1);
        if (ptr == nint.Zero)
            return false;

        var addon = (AtkUnitBase*)ptr;
        var node = GetNodeByIDChain(addon->GetRootNode(), ids);
        return node != null && node->IsVisible();
    }

    public static unsafe string GetNodeText(string addonName, params int[] nodeNumbers)
    {

        var ptr = Svc.GameGui.GetAddonByName(addonName, 1);

        var addon = (AtkUnitBase*)ptr;
        var uld = addon->UldManager;

        AtkResNode* node = null;
        var debugString = string.Empty;
        for (var i = 0; i < nodeNumbers.Length; i++)
        {
            var nodeNumber = nodeNumbers[i];

            var count = uld.NodeListCount;

            node = uld.NodeList[nodeNumber];
            debugString += $"[{nodeNumber}]";

            // More nodes to traverse
            if (i < nodeNumbers.Length - 1)
            {
                uld = ((AtkComponentNode*)node)->Component->UldManager;
            }
        }

        if (node->Type == NodeType.Counter)
            return ((AtkCounterNode*)node)->NodeText.ToString();

        var textNode = (AtkTextNode*)node;
        return textNode->NodeText.GetText();
    }

    public static unsafe AtkTextNode* GetAtkTextNode(string addonName, params int[] nodeNumbers)
    {

        var ptr = Svc.GameGui.GetAddonByName(addonName, 1);

        var addon = (AtkUnitBase*)ptr;
        var uld = addon->UldManager;

        AtkResNode* node = null;
        var debugString = string.Empty;
        for (var i = 0; i < nodeNumbers.Length; i++)
        {
            var nodeNumber = nodeNumbers[i];

            var count = uld.NodeListCount;

            node = uld.NodeList[nodeNumber];
            debugString += $"[{nodeNumber}]";

            // More nodes to traverse
            if (i < nodeNumbers.Length - 1)
            {
                uld = ((AtkComponentNode*)node)->Component->UldManager;
            }
        }

        var textNode = (AtkTextNode*)node;
        return textNode;
    }

    private static unsafe AtkResNode* GetNodeByIDChain(AtkResNode* node, params int[] ids)
    {
        if (node == null || ids.Length <= 0)
            return null;

        if (node->NodeId == ids[0])
        {
            if (ids.Length == 1)
                return node;

            var newList = new List<int>(ids);
            newList.RemoveAt(0);

            var childNode = node->ChildNode;
            if (childNode != null)
                return GetNodeByIDChain(childNode, [.. newList]);

            if ((int)node->Type >= 1000)
            {
                var componentNode = node->GetAsAtkComponentNode();
                var component = componentNode->Component;
                var uldManager = component->UldManager;
                childNode = uldManager.NodeList[0];
                return childNode == null ? null : GetNodeByIDChain(childNode, [.. newList]);
            }

            return null;
        }

        //check siblings
        var sibNode = node->PrevSiblingNode;
        return sibNode != null ? GetNodeByIDChain(sibNode, ids) : null;
    }

    #endregion

    #region LoadOnBoot

    public static unsafe void DictionaryCreation()
    {
        MoonRecipies = [];
        Svc.Data.GameData.Options.PanicOnSheetChecksumMismatch = false;

        var MoonMissionSheet = Svc.Data.GetExcelSheet<WKSMissionUnit>();
        var MoonRecipeSheet = Svc.Data.GetExcelSheet<WKSMissionRecipe>();
        var RecipeSheet = Svc.Data.GetExcelSheet<Recipe>();
        var ItemSheet = Svc.Data.GetExcelSheet<Item>();
        var ExpSheet = Svc.Data.GetExcelSheet<WKSMissionReward>();
        var ToDoSheet = Svc.Data.GetExcelSheet<WKSMissionToDo>();
        var MoonItemInfo = Svc.Data.GetExcelSheet<WKSItemInfo>();

        foreach (var item in MoonMissionSheet)
        {
            List<(int Type, int Amount)> Exp = [];
            Dictionary<ushort, int> MainItems = [];
            Dictionary<ushort, int> PreCrafts = [];
            uint keyId = item.RowId;
            string LeveName = item.Item.ToString();
            LeveName = LeveName.Replace("<nbsp>", " ");
            LeveName = LeveName.Replace("<->", "");

            if (LeveName == "")
                continue;

            int JobId = item.Unknown1 - 1;
            int Job2 = item.Unknown2;
            if (item.Unknown2 != 0)
            {
                Job2 = Job2 - 1;
            }

            uint silver = item.SilverStarRequirement;
            uint gold = item.GoldStarRequirement;
            uint previousMissionId = item.Unknown10;

            uint timeAndWeather = item.Unknown18;
            uint time = 0;
            CosmicWeather weather = CosmicWeather.FairSkies;
            if (timeAndWeather <= 12)
            {
                time = timeAndWeather;
            }
            else
            {
                weather = (CosmicWeather)(timeAndWeather - 12);
            }

            uint rank = item.Unknown17;
            bool isCritical = item.Unknown20;

            uint RecipeId = item.WKSMissionRecipe;

            uint toDoValue = item.Unknown7;
            if (CrafterJobList.Contains(JobId))
            {
                bool preCraftsbool = false;

                var toDoRow = ToDoSheet.GetRow(toDoValue);
                if (toDoRow.Unknown3 != 0) // shouldn't be 0, 1st item entry
                {
                    var item1Amount = toDoRow.Unknown6;
                    var item1Id = MoonItemInfo.GetRow(toDoRow.Unknown3).Item;
                    var item1Name = ItemSheet.GetRow(item1Id).Name.ToString();
                    var item1RecipeRow = RecipeSheet.Where(e => e.ItemResult.RowId == item1Id)
                                                    .Where(e => e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[0].Value.RowId ||
                                                                e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[1].Value.RowId ||
                                                                e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[2].Value.RowId ||
                                                                e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[3].Value.RowId ||
                                                                e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[4].Value.RowId)
                                                    .First();
                    var craftingType = item1RecipeRow.CraftType.Value.RowId;
                    PluginDebug($"Recipe Row ID: {item1RecipeRow.RowId} | for item: {item1Id}");
                    for (var i = 0; i <= 5; i++)
                    {
                        var subitem = item1RecipeRow.Ingredient[i].Value.RowId;
                        if (subitem != 0)
                            PluginDebug($"subItemId: {subitem} slot [{i}]");

                        if (subitem != 0)
                        {
                            var subitemRecipe = RecipeSheet.Where(x => x.ItemResult.RowId == subitem)
                                                           .Where(e => e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[0].Value.RowId ||
                                                                  e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[1].Value.RowId ||
                                                                  e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[2].Value.RowId ||
                                                                  e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[3].Value.RowId ||
                                                                  e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[4].Value.RowId)
                                                           .FirstOrDefault();
                            if (subitemRecipe.RowId != 0)
                            {
                                var subItemAmount = item1RecipeRow.AmountIngredient[i].ToInt();
                                subItemAmount = subItemAmount * item1Amount;
                                PreCrafts.Add(((ushort)subitemRecipe.RowId), subItemAmount);
                                preCraftsbool = true;
                            }
                        }
                    }
                    var item1RecipeId = item1RecipeRow.RowId;
                    MainItems.Add(((ushort)item1RecipeId), item1Amount);
                }
                if (toDoRow.Unknown4 != 0) // 2nd item entry
                {
                    var item2Amount = toDoRow.Unknown7;
                    var item2Id = MoonItemInfo.GetRow(toDoRow.Unknown4).Item;
                    var item2Name = ItemSheet.GetRow(item2Id).Name.ToString();

                    var item2RecipeRow = RecipeSheet.Where(e => e.ItemResult.RowId == item2Id)
                                                    .Where(e => e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[0].Value.RowId ||
                                                           e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[1].Value.RowId ||
                                                           e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[2].Value.RowId ||
                                                           e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[3].Value.RowId ||
                                                           e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[4].Value.RowId)
                                                    .First();
                    PluginDebug($"Recipe Row ID: {item2RecipeRow.RowId} | for item: {item2Id}");
                    for (var i = 0; i <= 5; i++)
                    {
                        var subitem = item2RecipeRow.Ingredient[i].Value.RowId;
                        if (subitem != 0)
                            PluginDebug($"subItemId: {subitem} slot [{i}]");

                        if (subitem != 0)
                        {
                            var subitemRecipe = RecipeSheet.Where(e => e.ItemResult.RowId == item2Id)
                                                           .Where(e => e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[0].Value.RowId ||
                                                                  e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[1].Value.RowId ||
                                                                  e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[2].Value.RowId ||
                                                                  e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[3].Value.RowId ||
                                                                  e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[4].Value.RowId)
                                                           .First();
                            if (subitemRecipe.RowId != 0)
                            {
                                var subItemAmount = item2RecipeRow.AmountIngredient[i].ToInt();
                                subItemAmount = subItemAmount * item2Amount;
                                PreCrafts.Add(((ushort)subitemRecipe.RowId), subItemAmount);
                                preCraftsbool = true;
                            }
                        }
                    }
                    var item2RecipeId = item2RecipeRow.RowId;
                    MainItems.Add(((ushort)item2RecipeId), item2Amount);
                }
                if (toDoRow.Unknown5 != 0) // 3rd item entry
                {
                    var item3Amount = toDoRow.Unknown8;
                    var item3Id = MoonItemInfo.GetRow(toDoRow.Unknown5).Item;
                    var item3Name = ItemSheet.GetRow(item3Id).Name.ToString();

                    var item3RecipeRow = RecipeSheet.Where(e => e.ItemResult.RowId == item3Id)
                                                    .Where(e => e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[0].Value.RowId ||
                                                           e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[1].Value.RowId ||
                                                           e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[2].Value.RowId ||
                                                           e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[3].Value.RowId ||
                                                           e.RowId == MoonRecipeSheet.GetRow(RecipeId).Recipe[4].Value.RowId)
                                                    .First();
                    PluginDebug($"Recipe Row ID: {item3RecipeRow.RowId} | for item: {item3Id}");
                    for (var i = 0; i <= 5; i++)
                    {
                        var subitem = item3RecipeRow.Ingredient[i].Value.RowId;
                        if (subitem != 0)
                            PluginDebug($"subItemId: {subitem} slot [{i}]");

                        if (subitem != 0)
                        {
                            var subitemRecipe = RecipeSheet.FirstOrDefault(x => x.ItemResult.RowId == subitem);
                            if (subitemRecipe.RowId != 0)
                            {
                                var subItemAmount = item3RecipeRow.AmountIngredient[i].ToInt();
                                subItemAmount = subItemAmount * item3Amount;
                                PreCrafts.Add(((ushort)subitemRecipe.RowId), subItemAmount);
                                preCraftsbool = true;
                            }
                        }
                    }
                    var item3RecipeId = item3RecipeRow.RowId;
                    MainItems.Add(((ushort)item3RecipeId), item3Amount);
                }

                if (preCraftsbool)
                {
                    foreach (var preItem in PreCrafts)
                    {
                        if (MainItems.ContainsKey(preItem.Key))
                            PreCrafts.Remove(preItem.Key);
                    }

                    if (PreCrafts.Count == 0)
                    {
                        preCraftsbool = false;
                    }
                }

                if (!MoonRecipies.ContainsKey(keyId))
                {
                    MoonRecipies[keyId] = new MoonRecipieInfo()
                    {
                        MainCraftsDict = MainItems,
                        PreCraftDict = PreCrafts,
                        PreCrafts = preCraftsbool
                    };
                }

            }

            // Col 3 -> Cosmocredits - Unknown 0
            // Col 4 -> Lunar Credits - Unknown 1
            // Col 7 ->  Lv. 1 Type - Unknown 12
            // Col 8 ->  Lv. 1 Exp - Unknown 2
            // Col 10 -> Lv. 2 Type - Unknown 13
            // Col 11 -> Lv. 2 Exp - Unknown 3
            // Col 13 -> Lv. 3 Type - Unknown 14
            // Col 14 -> Lv. 3 Exp - Unknown 4

            uint Cosmo = ExpSheet.GetRow(keyId).Unknown0;
            uint Lunar = ExpSheet.GetRow(keyId).Unknown1;

            if (ExpSheet.GetRow(keyId).Unknown2 != 0)
            {
                Exp.Add((ExpSheet.GetRow(keyId).Unknown12, ExpSheet.GetRow(keyId).Unknown2));
            }
            if (ExpSheet.GetRow(keyId).Unknown3 != 0)
            {
                Exp.Add((ExpSheet.GetRow(keyId).Unknown13, ExpSheet.GetRow(keyId).Unknown3));
            }
            if (ExpSheet.GetRow(keyId).Unknown4 != 0)
            {
                Exp.Add((ExpSheet.GetRow(keyId).Unknown14, ExpSheet.GetRow(keyId).Unknown4));
            }

            if (!MissionInfoDict.ContainsKey(keyId))
            {
                MissionInfoDict[keyId] = new MissionListInfo()
                {
                    Name = LeveName,
                    JobId = ((uint)JobId),
                    JobId2 = ((uint)Job2),
                    ToDoSlot = toDoValue,
                    Rank = rank,
                    IsCriticalMission = isCritical,
                    Time = time,
                    Weather = weather,
                    RecipeId = RecipeId,
                    SilverRequirement = silver,
                    GoldRequirement = gold,
                    CosmoCredit = Cosmo,
                    LunarCredit = Lunar,
                    ExperienceRewards = Exp,
                    PreviousMissionID = previousMissionId
                };
            }
        }

        if (C.Missions.Count == 0)
        {
            // fresh install?
            C.Missions = [.. MissionInfoDict.Select(x => new CosmicMission()
            {
                Id = x.Key,
                Name = x.Value.Name,
                Type = GetMissionType(x.Value),
                PreviousMissionId = x.Value.PreviousMissionID,
                JobId = x.Value.JobId,
            })];
            C.Save();
        }
        else
        {
            var newMissions = MissionInfoDict.Where(x => !C.Missions.Any(y => y.Id == x.Key)).Select(x => new CosmicMission()
            {
                Id = x.Key,
                Name = x.Value.Name,
                Type = GetMissionType(x.Value),
                PreviousMissionId = x.Value.PreviousMissionID,
                JobId = x.Value.JobId,
            });

            if (newMissions.Any())
            {
                C.Missions.AddRange(newMissions);
                C.Save();
            }
        }

        /*
        C.CriticalMissions = MissionInfoDict
                                .Where(m => m.Value.IsCriticalMission)
                                .Select(mission => (Id: mission.Key, Name: mission.Value.Name))
                                .ToList();
        C.TimedMissions = MissionInfoDict
                                .Where(m => m.Value.Time != 0)
                                .Select(mission => (Id: mission.Key, Name: mission.Value.Name))
                                .ToList();
        C.WeatherMissions = MissionInfoDict
                                .Where(m => m.Value.Weather != CosmicWeather.FairSkies)
                                .Where(m => !m.Value.IsCriticalMission)
                                .Select(mission => (Id: mission.Key, Name: mission.Value.Name))
                                .ToList();
        C.SequenceMissions = MissionInfoDict
                                .Where(m => m.Value.PreviousMissionID != 0)
                                .Select(mission => (Id: mission.Key, Name: mission.Value.Name))
                                .ToList();
        C.StandardMissions = MissionInfoDict
                                .Where(m => Ranks.Contains(m.Value.Rank) || ARankIds.Contains(m.Value.Rank))
                                .Where(m => !m.Value.IsCriticalMission)
                                .Where(m => m.Value.Time == 0)
                                .Where(m => m.Value.Weather == CosmicWeather.FairSkies)
                                .Select(mission => (Id: mission.Key, Name: mission.Value.Name))
                                .ToList();
        */
    }

    private static MissionType GetMissionType(MissionListInfo mission)
    {
        if (mission.IsCriticalMission)
        {
            return MissionType.Critical;
        }
        else if (mission.Time != 0)
        {
            return MissionType.Timed;
        }
        else if (mission.Weather != CosmicWeather.FairSkies)
        {
            return MissionType.Weather;
        }
        else if (mission.PreviousMissionID != 0)
        {
            return MissionType.Sequential;
        }

        return MissionType.Standard;
    }

    #endregion

    #region Useful Functions

    public static Vector3 RoundVector3(Vector3 v, int decimals)
    {
        return new Vector3(
            (float)Math.Round(v.X, decimals),
            (float)Math.Round(v.Y, decimals),
            (float)Math.Round(v.Z, decimals)
        );
    }

    public static unsafe void SetGatheringRing(uint teri, int x, int y, int radius)
    {
        var agent = AgentMap.Instance();
        var debugText = "Current teri/map: {currentTeri} {currentMap}" + ", " + agent->CurrentTerritoryId
                       + ", " + agent->CurrentMapId;
        PluginDebug(debugText);

        var terSheet = Svc.Data.GetExcelSheet<TerritoryType>();
        var mapId = terSheet.GetRow(teri).Map.Value.RowId;

        agent->IsFlagMarkerSet = false;
        agent->SetFlagMapMarker(teri, mapId, x, y);
        agent->AddGatheringTempMarker(x, y, radius, tooltip: "Node Location");
        agent->OpenMap(agent->CurrentMapId, teri, "Node Location", FFXIVClientStructs.FFXIV.Client.UI.Agent.MapType.GatheringLog);
    }


    #endregion

    #region Cosmic Exploration Display

    public static Dictionary<int, String> ExpDictionary = new Dictionary<int, String>
    {
        { 1, "I" },
        { 2, "II" },
        { 3, "III" },
        { 4, "IV" }
    };

    #endregion
}
