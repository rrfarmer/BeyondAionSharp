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
/// @author Mr. Poke
/// </summary>
public class _2208MauInTenMinutesADay : AbstractQuestHandler
{
    public _2208MauInTenMinutesADay() : base(2208)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203591).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203591).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203589).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(182203205, questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203591)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else if (env.GetDialogActionId() == DialogAction.QUEST_ACCEPT_1)
                {
                    if (GiveQuestItem(env, 182203205, 1))
                        return SendQuestStartDialog(env);
                    return true;
                }
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 203589)
            {
                int var = qs.GetQuestVarById(0);
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                {
                    if (var == 0)
                        return SendQuestDialog(env, 1693);
                    else if (var == 1)
                        return SendQuestDialog(env, 1352);
                }
                else if (env.GetDialogActionId() == DialogAction.SETPRO1)
                {
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return SendQuestSelectionDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203591)
                return SendQuestEndDialog(env);
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        int id = item.GetItemTemplate().GetTemplateId();
        int itemObjId = item.GetObjectId();

        if (id != 182203205)
            return HandlerResult.UNKNOWN;
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return HandlerResult.FAILED;
        PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 3000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            player.GetInventory().DecreaseByObjectId(itemObjId, 1);
            qs.SetQuestVarById(0, 1);
            UpdateQuestStatus(env);
            return ValueTask.CompletedTask;
        }, 3000L);
        return HandlerResult.SUCCESS;
    }
}
