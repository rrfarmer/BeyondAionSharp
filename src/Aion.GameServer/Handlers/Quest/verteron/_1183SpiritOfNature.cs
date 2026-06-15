using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Balthazar
/// </summary>
public class _1183SpiritOfNature : AbstractQuestHandler
{
    public _1183SpiritOfNature() : base(1183)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(730012).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(730012).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730013).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730014).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 730012)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 730013:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 1352);
                        case DialogAction.SETPRO1:
                            if (player.GetInventory().GetItemCountByItemId(182200550) == 0)
                                if (!GiveQuestItem(env, 182200550, 1))
                                    return true;
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                    }
                    return false;
                case 730014:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1693);
                        case DialogAction.SETPRO2:
                            if (player.GetInventory().GetItemCountByItemId(182200565) == 0)
                                if (!GiveQuestItem(env, 182200565, 1))
                                    return true;
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        default:
                            return SendQuestEndDialog(env);
                    }
                case 730012:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 2375);
                        case DialogAction.SELECT_QUEST_REWARD:
                            qs.SetQuestVar(3);
                            RemoveQuestItem(env, 182200550, 1);
                            RemoveQuestItem(env, 182200565, 1);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            return SendQuestEndDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 730012)
            {
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
