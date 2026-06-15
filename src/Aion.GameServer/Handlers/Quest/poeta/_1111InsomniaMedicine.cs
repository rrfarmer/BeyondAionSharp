using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke
/// </summary>
public class _1111InsomniaMedicine : AbstractQuestHandler
{
    public _1111InsomniaMedicine() : base(1111)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203075).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203075).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203061).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 203075)
        {
            if (qs == null)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                {
                    if (qs.GetQuestVarById(0) == 2)
                    {
                        RemoveQuestItem(env, 182200222, 1);
                        return SendQuestDialog(env, 2375);
                    }
                    else if (qs.GetQuestVarById(0) == 3)
                    {
                        qs.SetRewardGroup(1);
                        RemoveQuestItem(env, 182200221, 1);
                        return SendQuestDialog(env, 2716);
                    }
                    return false;
                }
                return SendQuestEndDialog(env);
            }
        }
        else if (targetId == 203061)
        {
            if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
            {
                if (qs.GetQuestVarById(0) == 0)
                    return SendQuestDialog(env, 1352);
                else if (qs.GetQuestVarById(0) == 1)
                    return SendQuestDialog(env, 1353);
                return false;
            }
            else if (env.GetDialogActionId() == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
            {
                if (QuestService.CollectItemCheck(env, true))
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    return SendQuestDialog(env, 1353);
                }
                else
                    return SendQuestDialog(env, 1693);
            }
            else if (env.GetDialogActionId() == DialogAction.SETPRO1 && qs.GetStatus() != QuestStatus.COMPLETE)
            {
                if (!GiveQuestItem(env, 182200222, 1))
                    return true;
                qs.SetQuestVarById(0, 2);
                qs.SetRewardGroup(0);
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                return true;
            }
            else if (env.GetDialogActionId() == DialogAction.SETPRO2 && qs.GetStatus() != QuestStatus.COMPLETE)
            {
                if (!GiveQuestItem(env, 182200221, 1))
                    return true;
                qs.SetQuestVarById(0, 3);
                qs.SetRewardGroup(1);
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                return true;
            }
        }
        return false;
    }
}
