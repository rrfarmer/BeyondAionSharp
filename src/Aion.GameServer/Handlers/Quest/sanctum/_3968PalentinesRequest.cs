using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rolandas
/// </summary>
public class _3968PalentinesRequest : AbstractQuestHandler
{
    public _3968PalentinesRequest() : base(3968)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(798390).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(798176).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204528).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203927).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798390).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc)
            targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 798390)
        {
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);

        if (targetId == 798176)
        {
            if (qs.GetStatus() == QuestStatus.START && var == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    if (GiveQuestItem(env, 182206123, 1))
                    {
                        qs.SetQuestVar(++var);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                    }
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 204528)
        {
            if (qs.GetStatus() == QuestStatus.START && var == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    if (GiveQuestItem(env, 182206124, 1))
                    {
                        qs.SetQuestVar(++var);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                    }
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 203927)
        {
            if (qs.GetStatus() == QuestStatus.START && var == 2)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2034);
                else if (env.GetDialogActionId() == DialogAction.SETPRO3)
                {
                    if (GiveQuestItem(env, 182206125, 1))
                    {
                        qs.SetQuestVar(++var);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                    }
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 798390)
        {
            if (env.GetDialogActionId() == DialogAction.USE_OBJECT && qs.GetStatus() == QuestStatus.REWARD)
                return SendQuestDialog(env, 2375);
            else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
            {
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
                RemoveQuestItem(env, 182206123, 1);
                RemoveQuestItem(env, 182206124, 1);
                RemoveQuestItem(env, 182206125, 1);
                return SendQuestEndDialog(env);
            }
            else
                return SendQuestEndDialog(env);
        }
        return false;
    }
}
