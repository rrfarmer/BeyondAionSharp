using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.GlobalDrops;
using Aion.GameServer.Services.Drop;
using Aion.GameServer.Services.Event;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/DropInfo (Oliver, AionCool, Bobobear, Neon). Shows drop information of your target.</summary>
public class DropInfo : AdminCommand
{
    public DropInfo()
        : base("dropinfo", "Shows drop information of your target.")
    {
        SetSyntaxInfo("[all] - Lists drops of the selected npc (default: only drops for your level range, optional: all possible drops).");
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        VisibleObject visibleObject = player.GetTarget();

        if (!(visibleObject is Npc npc))
        {
            SendInfo(player);
            return;
        }

        bool showAll = paramsArr.Length > 0 && paramsArr[0].Equals("all");
        NpcDrop npcDrop = DataManager.CUSTOM_NPC_DROP.GetNpcDrop(npc.GetNpcId());
        DropModifiers dropModifiers = DropRegistrationService.GetInstance().CreateDropModifiers(npc, player, player.GetLevel());
        dropModifiers.SetMaxDropsPerGroup(int.MaxValue);

        int[] counts = { 0, 0 };
        string info = "[" + npc.GetObjectTemplate().GetL10n() + "'s drops]";
        if (npcDrop != null)
        {
            foreach (DropGroup dropGroup in npcDrop.GetDropGroup())
            {
                if (dropGroup.GetRace() == Race.PC_ALL || dropGroup.GetRace() == dropModifiers.GetDropRace())
                {
                    info += "\nCustom drop group: " + dropGroup.GetName() + ", max drops: " + dropGroup.GetMaxItems();
                    counts[1]++;
                    foreach (Drop drop in dropGroup.GetDrop())
                    {
                        float finalChance = dropModifiers.CalculateDropChance(drop.GetChance(), dropGroup.IsUseLevelBasedChanceReduction());
                        if (!showAll && finalChance <= 0)
                            continue;
                        info += "\n\t" + ChatUtil.Item(drop.GetItemId()) + "\tBase chance: " + drop.GetChance() + "%, effective: " + finalChance + "%";
                        counts[0]++;
                    }
                }
            }
        }

        // if npc ai == quest_use_item it will be always excluded from global drops
        bool isNpcQuest = npc.GetAi().GetName().Equals("quest_use_item");
        if (!isNpcQuest)
        {
            bool hasGlobalNpcExclusions = DropRegistrationService.GetInstance().HasGlobalNpcExclusions(npc);
            bool isAllowedDefaultGlobalDropNpc = DropRegistrationService.GetInstance().IsAllowedDefaultGlobalDropNpc(npc, dropModifiers.IsDropNpcChest());
            // instances with WorldDropType.NONE must not have global drops (example Arenas)
            if (!hasGlobalNpcExclusions && npc.GetWorldDropType() != WorldDropType.NONE)
            {
                info += CollectDropInfo("Global", DataManager.GLOBAL_DROP_DATA.GetAllRules(), npc, dropModifiers, isAllowedDefaultGlobalDropNpc, showAll,
                    counts);
            }
            if (!hasGlobalNpcExclusions || dropModifiers.IsDropNpcChest())
                info += CollectDropInfo("Event", EventService.GetInstance().GetActiveEventDropRules(), npc, dropModifiers, isAllowedDefaultGlobalDropNpc,
                    showAll, counts);
        }

        info += "\n" + counts[0] + " total drops available in " + counts[1] + " drop groups" + (showAll ? "." : " on your level.");
        SendInfo(player, info);
    }

    private string CollectDropInfo(string dropGroupPrefix, List<GlobalRule> rules, Npc npc, DropModifiers dropModifiers,
        bool isAllowedDefaultGlobalDropNpc, bool showAll, int[] counts)
    {
        string info = "";
        foreach (GlobalRule rule in rules)
        {
            // if getGlobalRuleNpcs() != null means drops are for specified npcs (like named drops) so the default restrictions will be ignored
            if (isAllowedDefaultGlobalDropNpc || rule.GetGlobalRuleNpcs() != null)
            {
                float chance = DropRegistrationService.GetInstance().CalculateEffectiveChance(rule, npc, dropModifiers);
                if (!showAll && chance <= 0)
                    continue;

                List<GlobalDropItem> drops = DropRegistrationService.GetInstance().CollectDrops(rule, npc, dropModifiers);
                if (drops.Count != 0)
                {
                    info += "\n" + dropGroupPrefix + " drop group: \"" + rule.GetRuleName() + "\", max drops: " + rule.GetMaxDropRule();
                    if (rule.GetMemberLimit() != 1)
                        info += ", member limit: " + rule.GetMemberLimit();
                    info += "\n\tBase chance: " + rule.GetChance() + "%, effective: " + chance + "%";
                    counts[1]++;
                    foreach (GlobalDropItem item in drops)
                    {
                        info += "\n\t" + ChatUtil.Item(item.GetId());
                        if (item.GetChance() != 100f)
                            info += "\tSub chance: " + item.GetChance() + "%";
                        counts[0]++;
                    }
                }
            }
        }
        return info;
    }
}
