using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Questengine.Model;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Questengine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/KillInZone (Cheatkiller, Majka, Pad). Set.addAll→UnionWith; Set.equals→SetEquals; isEmpty→Count==0; super.onDialogEvent→base; DataManager.ZONE_DATA.zoneList→ZoneList; DataManager/QuestService red-tolerated.</summary>
public class KillInZone : AbstractTemplateQuestHandler
{
    private static readonly ILogger log = NullLogger.Instance;

    private readonly HashSet<int> startNpcIds = new();
    private readonly HashSet<int> endNpcIds = new();
    private readonly HashSet<string> zones = new();
    private readonly int killAmount;
    private readonly int minRank;
    private readonly int levelDiff;
    private readonly int startDistanceNpc;
    private readonly bool isDataDriven;

    public KillInZone(int questId, List<int> endNpcIds, List<int> startNpcIds, List<string> zones, int killAmount, int minRank, int levelDiff,
        int startDistanceNpc) : base(questId)
    {
        if (startNpcIds != null)
            this.startNpcIds.UnionWith(startNpcIds);
        if (endNpcIds != null)
            this.endNpcIds.UnionWith(endNpcIds);
        else
            this.endNpcIds.UnionWith(startNpcIds);
        if (zones != null)
        {
            this.zones.UnionWith(zones);
        }
        else
        {
            foreach (ZoneTemplate template in DataManager.ZONE_DATA.ZoneList)
                this.zones.Add(template.GetXmlName());
        }
        if (killAmount == 0)
            this.killAmount = 1;
        else
            this.killAmount = killAmount;
        this.minRank = minRank;
        this.levelDiff = levelDiff;
        this.startDistanceNpc = startDistanceNpc;
        this.isDataDriven = DataManager.QUEST_DATA.GetQuestById(questId).IsDataDriven();
        if (workItems != null)
            log.LogWarning("Q{QuestId} should not have work items", questId);
    }

    public override void Register()
    {
        foreach (int startNpcId in startNpcIds)
        {
            qe.RegisterQuestNpc(startNpcId).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(startNpcId).AddOnTalkEvent(questId);
        }
        if (!endNpcIds.SetEquals(startNpcIds))
        {
            foreach (int endNpcId in endNpcIds)
                qe.RegisterQuestNpc(endNpcId).AddOnTalkEvent(questId);
        }
        foreach (string zone in zones)
            qe.RegisterOnKillInZone(zone, questId);
        if (startDistanceNpc != 0)
            qe.RegisterQuestNpc(startDistanceNpc, 300).AddOnAtDistanceEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (startNpcIds.Count == 0 || startNpcIds.Contains(targetId))
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, isDataDriven ? 4762 : 1011);
                    case DialogAction.QUEST_ACCEPT:
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env);
                    default:
                        return base.OnDialogEvent(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (endNpcIds.Contains(targetId))
            {
                if (isDataDriven && dialogActionId == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillInZoneEvent(QuestEnv env)
    {
        // Rank restriction
        if (minRank > 0 && ((Player)env.GetVisibleObject()).GetAbyssRank().GetRank().GetId() < minRank)
            return false;
        // Level restriction
        if (levelDiff > 0 && (env.GetPlayer().GetLevel() - ((Player)env.GetVisibleObject()).GetLevel()) > levelDiff)
            return false;
        return DefaultOnKillInZoneEvent(env, 0, killAmount, true, isDataDriven); // reward
    }

    public override bool OnAtDistanceEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            QuestService.StartQuest(env);
            return true;
        }
        return false;
    }
}
