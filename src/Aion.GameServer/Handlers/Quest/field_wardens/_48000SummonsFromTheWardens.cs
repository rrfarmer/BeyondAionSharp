using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author vlog
/// </summary>
public class _48000SummonsFromTheWardens : AbstractQuestHandler
{
    public _48000SummonsFromTheWardens() : base(48000)
    {
    }

    public override void Register()
    {
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestNpc(799845).AddOnTalkEvent(questId);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (player.GetLevel() >= 30 && (qs == null || qs.IsStartable()))
            QuestService.StartQuest(new QuestEnv(null, player, questId));
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 799845) // Deryk
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    ChangeQuestStep(env, 0, 0, true);
                    return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799845) // Deryk
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
