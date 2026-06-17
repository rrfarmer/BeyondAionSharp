using System.Threading.Tasks;
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
public class _1643TheStarOfHeiron : AbstractQuestHandler
{
    public _1643TheStarOfHeiron() : base(1643)
    {
    }

    public override void Register()
    {
        qe.RegisterOnEnterWorld(questId);
        qe.RegisterQuestNpc(204545).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204545).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204630).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(204614).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npcObj)
            targetId = npcObj.GetNpcId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204545)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 4762);
                    case DialogAction.ASK_QUEST_ACCEPT:
                    case DialogAction.QUEST_ACCEPT_1:
                        return SendQuestStartDialog(env, 182201764, 1);
                }
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 204630:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                if (qs.GetQuestVarById(0) == 0)
                                {
                                    return SendQuestDialog(env, 1011);
                                }
                                else if (qs.GetQuestVarById(0) == 2)
                                {
                                    return SendQuestDialog(env, 1693);
                                }
                                return false;
                            }
                        case DialogAction.SETPRO1:
                            {
                                qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                                RemoveQuestItem(env, 182201764, 1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 0));
                                SpawnForFiveMinutes(204614, player.GetWorldMapInstance(), (float)1591.4327, (float)2774.2283, (float)127.63001, (byte)0);
                                return true;
                            }
                        case DialogAction.SET_SUCCEED:
                            {
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 0));
                                return true;
                            }
                    }
                    return false;
                case 204614:
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                if (qs.GetQuestVarById(0) == 1)
                                {
                                    return SendQuestDialog(env, 1011);
                                }
                                return false;
                            }
                        case DialogAction.SETPRO1:
                            {
                                qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                Npc npc = (Npc)env.GetVisibleObject();
                                ThreadPoolManager.GetInstance().Schedule(ct =>
                                {
                                    npc.GetController().Delete();
                                    return ValueTask.CompletedTask;
                                }, 40000L);
                                return true;
                            }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204545)
            {
                if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnEnterWorldEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null)
        {
            return false;
        }

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (qs.GetQuestVarById(0) == 1)
            {
                qs.SetQuestVar(0);
                UpdateQuestStatus(env);
            }
        }
        return false;
    }
}
