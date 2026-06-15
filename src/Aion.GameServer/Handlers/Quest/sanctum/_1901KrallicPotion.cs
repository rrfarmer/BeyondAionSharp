using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author edynamic90
/// </summary>
public class _1901KrallicPotion : AbstractQuestHandler
{
    public _1901KrallicPotion() : base(1901)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203830).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203830).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798026).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798025).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203131).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798003).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203864).AddOnTalkEvent(questId);
        /* qe.setQuestItemIds(182204115).add(questId); */
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npcObj)
            targetId = npcObj.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (targetId == 203830) // Marmeia
        {
            if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                return SendQuestDialog(env, 1011);
            else
                return SendQuestStartDialog(env);
        }
        else
        {
            if (targetId == 203864)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2375);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
                {
                    return SendQuestEndDialog(env);
                }
                else if (qs != null && qs.GetStatus() == QuestStatus.REWARD)
                {
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 3398);
                    return SendQuestEndDialog(env);
                }
            }
            else if (qs != null)
            {
                if (qs.GetStatus() == QuestStatus.START)
                {
                    int var = qs.GetQuestVarById(0);
                    switch (targetId)
                    {
                        case 798026: // Kunberunerk
                            switch (env.GetDialogActionId())
                            {
                                case DialogAction.QUEST_SELECT:
                                    if (var == 0)
                                        return SendQuestDialog(env, 1352);
                                    else if (var == 5)
                                        return SendQuestDialog(env, 3057);
                                    return false;
                                case DialogAction.SELECT2_2:
                                    Storage inventory = player.GetInventory();
                                    if (inventory.TryDecreaseKinah(10000))
                                    {
                                        return SendQuestDialog(env, 1438);
                                    }
                                    else
                                        return SendQuestDialog(env, 1523);
                                case DialogAction.SETPRO1:
                                    qs.SetQuestVarById(0, 5); // Reward (5)
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                                    return true;
                                case DialogAction.SETPRO2:
                                    qs.SetQuestVarById(0, 1); // 1
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                                    return true;
                                case DialogAction.SETPRO7:
                                    qs.SetQuestVarById(0, 5); // 5
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                                    return true;
                                default:
                                    return SendQuestStartDialog(env);
                            }
                        case 798025: // Mapireck
                            switch (env.GetDialogActionId())
                            {
                                case DialogAction.QUEST_SELECT:
                                    if (var == 1)
                                        return SendQuestDialog(env, 1693);
                                    else if (var == 4)
                                        return SendQuestDialog(env, 2716);
                                    return false;
                                case DialogAction.SETPRO3:
                                    qs.SetQuestVarById(0, var + 1); // var==2
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                                    return true;
                                case DialogAction.SETPRO6:
                                    RemoveQuestItem(env, 182206000, 1);
                                    qs.SetQuestVarById(0, var + 1); // var==5
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                                    return true;
                            }
                            return false;
                        case 203131: // Maniparas
                            switch (env.GetDialogActionId())
                            {
                                case DialogAction.QUEST_SELECT:
                                    return SendQuestDialog(env, 2034);
                                case DialogAction.SETPRO4:
                                    qs.SetQuestVarById(0, var + 1); // var==3
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                                    return true;
                            }
                            return false;
                        case 798003: // Gaphyrk
                            switch (env.GetDialogActionId())
                            {
                                case DialogAction.QUEST_SELECT:
                                    return SendQuestDialog(env, 2375);
                                case DialogAction.SETPRO5:
                                    if (player.GetInventory().GetItemCountByItemId(182206000) == 0)
                                        if (!GiveQuestItem(env, 182206000, 1))
                                            return true;
                                    qs.SetQuestVarById(0, var + 1); // var==4
                                    UpdateQuestStatus(env);
                                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                                    return true;
                            }
                            break;
                    }
                }
            }
            return false;
        }
    }
}
