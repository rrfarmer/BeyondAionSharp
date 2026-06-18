using System;
using Aion.GameServer.Ai;
using Aion.GameServer.Configs;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Ingameshop;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.Services.Event;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>
/// Java parity: data/handlers/admincommands/Reload (MrPoke, Neon). Reloads templates or handlers (static data).
/// The faithful C# DataManager holders are read-only accessors delegating to StaticData; the operator-reload
/// contract is provided by the minimal settable reload surface added on StaticData/DataManager
/// (DataManager.ReloadXxx), which re-runs the same leaf loaders Java's Reload re-deserializes
/// (gameplay-faithful-infra-idiomatic — the reload entrypoint is infra, the reloaded data is faithful).
/// </summary>
public class Reload : AdminCommand
{
    public Reload()
        : base("reload", "Reloads templates or handlers (static data).")
    {
        SetSyntaxInfo(
            "<config> - Reloads all configuration settings.",
            "<commands|ai> - Reloads the specified handlers.",
            "<quests> - Reloads quest templates and handlers.",
            "<skills|npcskills> - Reloads the specified skill templates.",
            "<events> - Reloads event templates and (re)starts events.",
            "<arcade> - Reloads the Upgrade Arcade reward item list.",
            "<decomposables> - Reloads content of item bundles",
            "<items|customdrops|gameshop> - Reloads the specified data.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }
        if (paramsArr[0].Equals("quests", StringComparison.OrdinalIgnoreCase))
        {
            DataManager.ReloadQuestData();
            DataManager.ReloadXmlQuests();
            QuestEngine.QuestEngine.GetInstance().Reload();
            SendInfo(admin, DataManager.QUEST_DATA.Size() + " quest templates loaded (" + QuestEngine.QuestEngine.GetInstance().GetQuestHandlerCount() + " handlers).");
        }
        else if (paramsArr[0].Equals("skills", StringComparison.OrdinalIgnoreCase))
        {
            DataManager.ReloadSkillData();
            SendInfo(admin, DataManager.SKILL_DATA.Size() + " skills loaded.");
        }
        else if (paramsArr[0].Equals("npcskills", StringComparison.OrdinalIgnoreCase))
        {
            DataManager.ReloadNpcSkillData();
            SendInfo(admin, DataManager.NPC_SKILL_DATA.Size() + " npc skills loaded.");
        }
        else if (paramsArr[0].Equals("items", StringComparison.OrdinalIgnoreCase))
        {
            DataManager.ReloadItemData();
            SendInfo(admin, DataManager.ITEM_DATA.Size() + " item templates loaded.");
        }
        else if (paramsArr[0].Equals("ai", StringComparison.OrdinalIgnoreCase))
        {
            AIEngine.GetInstance().Reload();
            SendInfo(admin, "AI successfully reloaded!");
        }
        else if (paramsArr[0].Equals("commands", StringComparison.OrdinalIgnoreCase))
        {
            ChatProcessor.GetInstance().Reload();
            SendInfo(admin, "Chat commands successfully reloaded!");
        }
        else if (paramsArr[0].Equals("config", StringComparison.OrdinalIgnoreCase))
        {
            Config.Load();
            SendInfo(admin, "Configs successfully reloaded!");
        }
        else if (paramsArr[0].Equals("customdrops", StringComparison.OrdinalIgnoreCase))
        {
            DataManager.ReloadCustomDrop();
            SendInfo(admin, DataManager.CUSTOM_NPC_DROP.Size() + " custom drops loaded.");
        }
        else if (paramsArr[0].Equals("gameshop", StringComparison.OrdinalIgnoreCase))
        {
            InGameShopEn.GetInstance().Reload();
            SendInfo(admin, "Gameshop successfully reloaded!");
        }
        else if (paramsArr[0].Equals("events", StringComparison.OrdinalIgnoreCase))
        {
            EventService.GetInstance().Stop();
            DataManager.ReloadEventData();
            EventService.GetInstance().Start();
            SendInfo(admin, DataManager.EVENT_DATA.Size() + " events loaded.");
        }
        else if (paramsArr[0].Equals("arcade", StringComparison.OrdinalIgnoreCase))
        {
            DataManager.ReloadUpgradeArcadeData();
            long rewards = 0;
            foreach (var l in DataManager.UPGRADE_ARCADE_DATA.GetRewards())
                rewards += l.GetArcadeRewardItems().Count;
            SendInfo(admin, rewards + " upgrade arcade rewards loaded.");
        }
        else if (paramsArr[0].Equals("decomposables", StringComparison.OrdinalIgnoreCase))
        {
            DataManager.ReloadDecomposableItemsData();
            SendInfo(admin, DataManager.DECOMPOSABLE_ITEMS_DATA.Size() + " item bundles reloaded.");
        }
        else
            SendInfo(admin);
    }
}
