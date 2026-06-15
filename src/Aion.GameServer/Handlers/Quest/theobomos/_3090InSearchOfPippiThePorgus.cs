using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _3090InSearchOfPippiThePorgus : AbstractQuestHandler
{
    public _3090InSearchOfPippiThePorgus() : base(3090)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798182).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798182).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798193).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700420).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700421).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(206085).AddOnAtDistanceEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 798182)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 798193)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 0)
                    {
                        return SendQuestDialog(env, 1011);
                    }
                    else if (qs.GetQuestVarById(0) == 2)
                    {
                        return SendQuestDialog(env, 1693);
                    }
                }
                else if (dialogActionId == DialogAction.SETPRO1)
                {
                    return DefaultCloseDialog(env, 0, 1);
                }
                else if (dialogActionId == DialogAction.SETPRO3)
                {
                    if (player.GetInventory().GetKinah() >= 10000)
                    {
                        GiveQuestItem(env, 182208050, 1);
                        player.GetInventory().DecreaseKinah(10000);
                        return DefaultCloseDialog(env, 2, 3);
                    }
                    else
                        return SendQuestDialog(env, 1779);
                }
                else if (dialogActionId == DialogAction.SELECT3_2)
                {
                    return SendQuestDialog(env, 1779);
                }
            }
            if (targetId == 700420)
            {
                int var = qs.GetQuestVarById(0);
                int var2 = qs.GetQuestVarById(2);
                if (var == 1 && var2 == 0)
                {
                    ChangeQuestStep(env, 0, 1, false, 2);
                    PacketSendUtility.SendMonologue(player, 1111007);
                    ChangeStep(qs, env);
                    return true;
                }
            }
            if (targetId == 700421)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    if (qs.GetQuestVarById(0) == 3)
                    {
                        return SendQuestDialog(env, 2034);
                    }
                }
                else if (dialogActionId == DialogAction.SET_SUCCEED)
                {
                    env.GetVisibleObject().GetController().DeleteAndScheduleRespawn();
                    RemoveQuestItem(env, 182208050, 1);
                    GiveQuestItem(env, 182208051, 1);
                    return DefaultCloseDialog(env, 3, 3, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798182)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 10002);
                    default:
                    {
                        RemoveQuestItem(env, 182208051, 1);
                        return SendQuestEndDialog(env);
                    }
                }
            }
        }
        return false;
    }

    public override bool OnAtDistanceEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            int var1 = qs.GetQuestVarById(1);
            if (var == 1 && var1 == 0)
            {
                ChangeQuestStep(env, 0, 1, false, 1);
                PacketSendUtility.SendMonologue(player, 1111006);
                ChangeStep(qs, env);
                return true;
            }
        }
        return false;
    }

    private void ChangeStep(QuestState qs, QuestEnv env)
    {
        if (qs.GetQuestVarById(1) == 1 && qs.GetQuestVarById(2) == 1)
        {
            qs.SetQuestVarById(1, 0);
            qs.SetQuestVarById(2, 0);
            qs.SetQuestVar(2);
            UpdateQuestStatus(env);
        }
    }
}
