using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Rift;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Model.Vortex;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.QuestEngine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/KillInWorld (vlog, bobobear, Pad). Standard xml-based handling for DAILY quests with onKillInZone events. Set.addAll→UnionWith; Set.equals→SetEquals; isEmpty→Count==0; super.onDialogEvent→base; DataManager/QuestService/RiftService/VortexService red-tolerated.</summary>
public class KillInWorld : AbstractTemplateQuestHandler
{
    private static readonly ILogger log = NullLogger.Instance;

    private readonly HashSet<int> startNpcIds = new();
    private readonly HashSet<int> endNpcIds = new();
    private readonly HashSet<int> worldIds = new();
    private readonly int killAmount;
    private readonly int minRank;
    private readonly int levelDiff;
    private readonly int invasionWorldId;
    private readonly int startDialogId;
    private readonly int startDistanceNpcId;
    private readonly int endDialogId;
    private readonly bool isDataDriven;

    public KillInWorld(int questId, List<int> endNpcIds, List<int> startNpcIds, List<int> worldIds, int killAmount, int minRank,
        int levelDiff, int invasionWorld, int startDialogId, int startDistanceNpcId, int endDialogId) : base(questId)
    {
        if (startNpcIds != null)
            this.startNpcIds.UnionWith(startNpcIds);
        if (endNpcIds != null)
            this.endNpcIds.UnionWith(endNpcIds);
        else
            this.endNpcIds.UnionWith(this.startNpcIds);
        if (worldIds != null)
        {
            this.worldIds.UnionWith(worldIds);
        }
        else
        {
            foreach (WorldMapTemplate template in DataManager.WORLD_MAPS_DATA)
                this.worldIds.Add(template.GetMapId());
        }
        if (killAmount == 0)
            this.killAmount = 1;
        else
            this.killAmount = killAmount;
        this.minRank = minRank;
        this.levelDiff = levelDiff;
        this.invasionWorldId = invasionWorld;
        this.startDialogId = startDialogId;
        this.startDistanceNpcId = startDistanceNpcId;
        this.endDialogId = endDialogId;
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
        foreach (int worldId in worldIds)
            qe.RegisterOnKillInWorld(worldId, questId);

        if (invasionWorldId != 0)
            qe.RegisterOnEnterWorld(questId);

        if (startDistanceNpcId != 0)
            qe.RegisterQuestNpc(startDistanceNpcId, 300).AddOnAtDistanceEvent(questId);
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
                        return SendQuestDialog(env, startDialogId != 0 ? startDialogId : isDataDriven ? 4762 : 1011);
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
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, endDialogId != 0 ? endDialogId : isDataDriven ? 10002 : 2375);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnEnterWorldEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        VortexLocation vortexLoc = VortexService.GetInstance().GetLocationByWorld(invasionWorldId);
        if (player.GetWorldId() == invasionWorldId)
        {
            if (qs == null || qs.IsStartable())
            {
                if (vortexLoc != null && vortexLoc.IsActive() || SearchOpenRift())
                    return QuestService.StartQuest(env);
            }
        }
        return false;
    }

    private bool SearchOpenRift()
    {
        foreach (RiftLocation loc in RiftService.GetInstance().GetRiftLocations().Values)
        {
            if (loc.GetWorldId() == invasionWorldId && loc.IsOpened())
            {
                return true;
            }
        }
        return false;
    }

    public override bool OnKillInWorldEvent(QuestEnv env)
    {
        // Rank restriction
        if (minRank > 0 && ((Player)env.GetVisibleObject()).GetAbyssRank().GetRank().GetId() < minRank)
            return false;
        // Level restriction
        if (levelDiff > 0 && (env.GetPlayer().GetLevel() - ((Player)env.GetVisibleObject()).GetLevel()) > levelDiff)
            return false;
        return DefaultOnKillRankedEvent(env, 0, killAmount, true, isDataDriven); // reward
    }

    public override bool OnAtDistanceEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
            return QuestService.StartQuest(env);
        return false;
    }
}
