using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Go to Draupnir Cave in Asmodae and get Blue Balaur Blood (186000035) (2) and Balaur Rainbow Scales (186000036) (5) for Brosia (204601). Go to
/// Brosia to choose your reward.
///
/// @author Balthazar, vlog
/// </summary>
public class _1687TheTigrakiAgreement : AbstractQuestHandler
{
    public _1687TheTigrakiAgreement() : base(1687)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204601).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204601).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204601) // Brosia
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            if (targetId == 204601) // Brosia
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        long collect1 = player.GetInventory().GetItemCountByItemId(186000035);
                        long collect2 = player.GetInventory().GetItemCountByItemId(186000036);
                        if (collect1 >= 2 && collect2 >= 5)
                        {
                            RemoveQuestItem(env, 186000035, 2);
                            RemoveQuestItem(env, 186000036, 5);
                            return SendQuestDialog(env, 1352); // choose your reward
                        }
                        else
                            return SendQuestDialog(env, 1097);
                    case DialogAction.FINISH_DIALOG:
                        return DefaultCloseDialog(env, var, var);
                    case DialogAction.SETPRO10:
                        qs.SetRewardGroup(0);
                        return DefaultCloseDialog(env, var, var, true, true); // reward 1
                    case DialogAction.SETPRO20:
                        qs.SetRewardGroup(1);
                        return DefaultCloseDialog(env, var, var, true, true); // reward 2
                    case DialogAction.SETPRO30:
                        qs.SetRewardGroup(2);
                        return DefaultCloseDialog(env, var, var, true, true); // reward 3
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204601) // Brosia
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
