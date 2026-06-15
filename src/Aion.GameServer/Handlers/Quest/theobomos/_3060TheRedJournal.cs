using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

public class _3060TheRedJournal : AbstractQuestHandler
{
    public _3060TheRedJournal() : base(3060)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(730148).AddOnQuestStart(questId); // Red Journal
        qe.RegisterQuestNpc(730148).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798190).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798191).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798192).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(798193).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182208043, questId);
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
            if (targetId == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    QuestService.StartQuest(env);
                    return CloseDialogWindow(env);
                }
                if (env.GetDialogActionId() == DialogAction.QUEST_REFUSE_1)
                {
                    PacketSendUtility.SendPacket(player, new SmDialogWindow(0, 0));
                    return SendQuestEndDialog(env);
                }
            }
            else if (targetId == 730148)
            {
                return GiveQuestItem(env, 182208043, 1);
            }
        }
        else if (targetId == 798190)
        {
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 0)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1352);
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 798191)
        {
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 1)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1693);
                else if (env.GetDialogActionId() == DialogAction.SETPRO2)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 798192)
        {
            if (qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 2)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 2034);
                else if (env.GetDialogActionId() == DialogAction.SETPRO3)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    PacketSendUtility.SendPacket(player, new SmDialogWindow(env.GetVisibleObject().GetObjectId(), 10));
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (targetId == 798193)
        {
            if (env.GetDialogActionId() == DialogAction.QUEST_SELECT && qs.GetStatus() == QuestStatus.START)
            {
                return SendQuestDialog(env, 2375);
            }
            else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD && qs.GetStatus() != QuestStatus.COMPLETE)
            {
                RemoveQuestItem(env, 182208043, 1);
                qs.SetQuestVar(1);
                qs.SetStatus(QuestStatus.REWARD);
                UpdateQuestStatus(env);
                return SendQuestEndDialog(env);
            }
            else
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        int id = item.GetItemTemplate().GetTemplateId();
        int itemObjId = item.GetObjectId();

        if (id != 182208043)
            return HandlerResult.UNKNOWN;
        PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 3000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 3000, 1, 0), true);
            SendQuestDialog(env, 4);
            return ValueTask.CompletedTask;
        }, 3000L);
        return HandlerResult.SUCCESS;
    }
}
