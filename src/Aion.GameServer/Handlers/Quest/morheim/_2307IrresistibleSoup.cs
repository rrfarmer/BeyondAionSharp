using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author MrPoke, Nephis
/// </summary>
public class _2307IrresistibleSoup : AbstractQuestHandler
{
    public _2307IrresistibleSoup() : base(2307)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204378).AddOnQuestStart(questId); // Favyr
        qe.RegisterQuestNpc(204378).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204336).AddOnTalkEvent(questId); // Spedor
        qe.RegisterQuestNpc(700247).AddOnTalkEvent(questId); // Aromatic Soup
    }

    public override bool OnDialogEvent(QuestEnv env)
    { // INGREDIENT CHECK NEED TO SCRIPT
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 204378)
        { // Favyr
            if (qs == null || qs.IsStartable())
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                return SendQuestEndDialog(env);
            }
        }
        else if (targetId == 204336)
        { // Spedor
            if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    qs.SetQuestVar(2);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    RemoveQuestItem(env, 182204106, 1);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else if (env.GetDialogActionId() == DialogAction.SELECT1_3)
                {
                    qs.SetQuestVar(1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    RemoveQuestItem(env, 182204107, 1);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else if (env.GetDialogActionId() == DialogAction.SELECT1_4)
                {
                    qs.SetQuestVar(1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    RemoveQuestItem(env, 182204108, 1);
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
        {
            switch (targetId)
            {
                case 700247: // Aromatic Soup
                    if (qs.GetQuestVarById(0) == 0 && env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        qs.SetQuestVar(1);
                        UpdateQuestStatus(env);
                    }
                    break;
            }
        }
        return false;
    }
}
