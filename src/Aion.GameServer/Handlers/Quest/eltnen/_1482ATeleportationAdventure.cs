using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Balthazar
/// </summary>
public class _1482ATeleportationAdventure : AbstractQuestHandler
{
    public _1482ATeleportationAdventure() : base(1482)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203919).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203919).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203337).AddOnTalkEvent(questId);
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
            if (targetId == 203919)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
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
                case 203337:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                        {
                            switch (qs.GetQuestVarById(0))
                            {
                                case 0:
                                {
                                    return SendQuestDialog(env, 1011);
                                }
                                case 1:
                                {
                                    return SendQuestDialog(env, 1352);
                                }
                                case 2:
                                {
                                    return SendQuestDialog(env, 1693);
                                }
                                case 3:
                                {
                                    return SendQuestDialog(env, 10002);
                                }
                            }
                            return false;
                        }
                        case DialogAction.SETPRO1:
                        {
                            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                            UpdateQuestStatus(env);
                            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                            return true;
                        }
                        case DialogAction.SETPRO3:
                        {
                            qs.SetQuestVar(3);
                            qs.SetStatus(QuestStatus.REWARD);
                            UpdateQuestStatus(env);
                            TeleportService.TeleportTo(player, 220020000, 1, 638, 2337, 425, (byte)20);
                            return true;
                        }
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                        {
                            long itemCount1 = player.GetInventory().GetItemCountByItemId(182201399);
                            if (itemCount1 >= 3)
                            {
                                RemoveQuestItem(env, 182201399, itemCount1);
                                ChangeQuestStep(env, 1, 2);
                                return SendQuestDialog(env, 10000);
                            }
                            else
                                return SendQuestDialog(env, 10001);
                        }
                        default:
                            return SendQuestStartDialog(env);
                    }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203337)
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
