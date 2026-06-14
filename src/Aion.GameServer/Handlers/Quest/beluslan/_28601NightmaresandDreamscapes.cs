using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Gigi
/// </summary>
public class _28601NightmaresandDreamscapes : AbstractQuestHandler
{
    public _28601NightmaresandDreamscapes() : base(28601)
    {
    }

    public override void Register()
    {
        int[] npc_ids = { 204702, 205234 };
        foreach (int npc_id in npc_ids)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204702).AddOnQuestStart(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204702)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                {
                    return SendQuestStartDialog(env, 182213006, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 205234)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                {
                    return DefaultCloseDialog(env, 0, 0, true, true, 0, 0, 182213006, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 205234)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
