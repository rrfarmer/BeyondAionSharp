using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _2423ThereAndBackAgain : AbstractQuestHandler
{
    public _2423ThereAndBackAgain() : base(2423)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204326).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204375).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204326)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 204375:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            if (qs.GetQuestVarById(0) == 0)
                                return SendQuestDialog(env, 1011);
                            else if (qs.GetQuestVarById(0) == 1)
                                return SendQuestDialog(env, 1352);
                            else if (qs.GetQuestVarById(0) == 2)
                                return SendQuestDialog(env, 1693);
                            return false;
                        }
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        {
                            return CheckQuestItems(env, 1, 2, false, 10000, 10001);
                        }
                        case DialogAction.SETPRO3:
                        {
                            TeleportService.TeleportTo(player, 210020000, 1, 370.13f, 2682.59f, 171, (byte)30, TeleportAnimation.FADE_OUT_BEAM);
                            qs.SetQuestVar(3);
                            return DefaultCloseDialog(env, 3, 3, true, false);
                        }
                        case DialogAction.SELECT3_2:
                        {
                            return SendQuestDialog(env, 1779);
                        }
                        case DialogAction.SETPRO1:
                        {
                            TeleportService.TeleportTo(player, 210020000, 1, 370.13f, 2682.59f, 171, (byte)30, TeleportAnimation.FADE_OUT_BEAM);
                            return DefaultCloseDialog(env, 0, 1);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204375)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
