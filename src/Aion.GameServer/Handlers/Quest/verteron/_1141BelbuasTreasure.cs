using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr. Poke, Dune11, vlog
/// </summary>
public class _1141BelbuasTreasure : AbstractQuestHandler
{
    public _1141BelbuasTreasure() : base(1141)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(730001).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(730001).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700122).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 730001) // Nola
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 700122) // Belbua's Wine Barrel
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    ChangeQuestStep(env, 0, 0, true); // reward
                    return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 700122) // Belbua's Wine Barrel
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
