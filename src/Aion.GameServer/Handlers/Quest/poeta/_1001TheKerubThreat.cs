using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke, Majka
/// </summary>
public class _1001TheKerubThreat : AbstractQuestHandler
{
    public _1001TheKerubThreat() : base(1001)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(210670).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(203071).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203067).AddOnTalkEvent(questId);
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs.GetStatus() != QuestStatus.START)
            return false;
        if (targetId == 210670)
        {
            if (var > 0 && var < 6)
            {
                qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                UpdateQuestStatus(env);
                return true;
            }
        }
        return false;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 1100);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 1100);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203071)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.SELECT1_1:
                        PlayQuestMovie(env, 15);
                        return false;
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1011);
                        else if (var == 6)
                            return SendQuestDialog(env, 1352);
                        else if (var == 7)
                            return SendQuestDialog(env, 1693);
                        return false;
                    case DialogAction.SETPRO3:
                    case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        if (var == 7)
                        {
                            long itemCount = player.GetInventory().GetItemCountByItemId(182200001);
                            if (itemCount >= 3)
                            {
                                if (env.GetDialogActionId() == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                                {
                                    return SendQuestDialog(env, 1694);
                                }
                                else
                                {
                                    RemoveQuestItem(env, 182200001, itemCount);
                                    qs.SetQuestVarById(0, var + 1);
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                    return true;
                                }
                            }
                            else
                                return SendQuestDialog(env, 1779);
                        }
                        return true;
                    case DialogAction.SETPRO1:
                    case DialogAction.SETPRO2:
                        if (var == 0 || var == 6)
                        {
                            qs.SetQuestVarById(0, var + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                        }
                        return true;
                    default:
                        return false;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203067)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
