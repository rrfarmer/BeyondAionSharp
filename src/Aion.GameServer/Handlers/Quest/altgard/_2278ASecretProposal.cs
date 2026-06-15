using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Ritsu, Majka
/// </summary>
public class _2278ASecretProposal : AbstractQuestHandler
{
    public _2278ASecretProposal() : base(2278)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203590).AddOnQuestStart(questId); // Emgata
        qe.RegisterQuestNpc(203590).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203557).AddOnTalkEvent(questId); // Suthran
        qe.RegisterQuestNpc(204206).AddOnTalkEvent(questId); // Cavalorn
        qe.RegisterQuestNpc(204075).AddOnTalkEvent(questId); // Balder
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
        {
            targetId = npc.GetNpcId();
        }
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203590)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
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
            int var = qs.GetQuestVarById(0);
            if (targetId == 203557) // Suthran
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                        {
                            return SendQuestDialog(env, 1352);
                        }
                        if (var == 3)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestDialog(env, 2375);
                        }
                        break;
                    case DialogAction.SETPRO1:
                        if (var == 0)
                        {
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        break;
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestEndDialog(env);
                }
            }
            if (targetId == 204206) // Cavalorn
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                        {
                            return SendQuestDialog(env, 1693);
                        }
                        break;
                    case DialogAction.SETPRO2:
                        if (var == 1)
                        {
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        break;
                }
            }
            if (targetId == 204075) // Balder
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 2)
                        {
                            return SendQuestDialog(env, 2034);
                        }
                        break;
                    case DialogAction.SETPRO3:
                        if (var == 2)
                        {
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        break;
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203557)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
