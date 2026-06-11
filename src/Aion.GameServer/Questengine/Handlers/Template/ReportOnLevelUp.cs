using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.QuestEngine.Handlers.Template;

/// <summary>Java parity: questEngine/handlers/template/ReportOnLevelUp (Majka, Bobobear, Pad). Set.addAll→HashSet.UnionWith; QuestService red-tolerated.</summary>
public class ReportOnLevelUp : AbstractTemplateQuestHandler
{
    private readonly HashSet<int> endNpcIds = new();

    public ReportOnLevelUp(int questId, List<int> endNpcIds) : base(questId)
    {
        if (endNpcIds != null)
        {
            this.endNpcIds.UnionWith(endNpcIds);
        }
    }

    public override void Register()
    {
        foreach (int endNpcId in endNpcIds)
            qe.RegisterQuestNpc(endNpcId).AddOnTalkEvent(questId);

        qe.RegisterOnEnterWorld(questId);
        qe.RegisterOnLevelChanged(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();

        if (qs == null)
            return false;
        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (endNpcIds.Contains(targetId))
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnEnterWorldEvent(QuestEnv env)
    {
        return StartQuest(env.GetPlayer());
    }

    public override void OnLevelChangedEvent(Player player)
    {
        StartQuest(player);
    }

    private bool StartQuest(Player player)
    {
        if (!player.GetQuestStateList().HasQuest(questId))
            return QuestService.StartQuest(new QuestEnv(null, player, questId), QuestStatus.REWARD, false);
        return false;
    }
}
