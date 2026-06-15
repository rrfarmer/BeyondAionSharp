using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke
/// </summary>
public class _1122DeliveringPernossRobe : AbstractQuestHandler
{
    public _1122DeliveringPernossRobe() : base(1122)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203060).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203060).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(790001).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 203060)
        {
            if (qs == null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    if (GiveQuestItem(env, 182200216, 1))
                        return SendQuestStartDialog(env);
                    else
                        return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 790001)
        {
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                long itemCount;
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SETPRO1:
                        itemCount = player.GetInventory().GetItemCountByItemId(182200218);
                        if (itemCount > 0)
                        {
                            qs.SetQuestVar(1);
                            qs.SetRewardGroup(0);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            RemoveQuestItem(env, 182200218, 1);
                            RemoveQuestItem(env, 182200216, 1);
                            return SendQuestDialog(env, 1523);
                        }
                        else
                            return SendQuestDialog(env, 1608);

                    case DialogAction.SETPRO2:
                        itemCount = player.GetInventory().GetItemCountByItemId(182200219);
                        if (itemCount > 0)
                        {
                            qs.SetQuestVar(2);
                            qs.SetRewardGroup(1);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            RemoveQuestItem(env, 182200219, 1);
                            RemoveQuestItem(env, 182200216, 1);
                            return SendQuestDialog(env, 1438);
                        }
                        else
                            return SendQuestDialog(env, 1608);
                    case DialogAction.SETPRO3:
                        itemCount = player.GetInventory().GetItemCountByItemId(182200220);
                        if (itemCount > 0)
                        {
                            qs.SetQuestVar(3);
                            qs.SetRewardGroup(2);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            RemoveQuestItem(env, 182200220, 1);
                            RemoveQuestItem(env, 182200216, 1);
                            return SendQuestDialog(env, 1353);
                        }
                        else
                            return SendQuestDialog(env, 1608);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
            else if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
