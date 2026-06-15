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
/// @author Majka
/// </summary>
public class _25023SproutingDevelopments : AbstractQuestHandler
{
    public _25023SproutingDevelopments() : base(25023)
    {
    }

    public override void Register()
    {
        // Malthorn 804909
        // Varla 804910
        qe.RegisterQuestNpc(804909).AddOnQuestStart(questId);
        int[] npcs = { 804909, 804910 };
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
        qe.RegisterQuestItem(182215711, questId); // Enriched Seed
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 804909) // Malthorn
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);

            switch (targetId)
            {
                case 804909: // Malthorn
                    if (var == 0)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1011);

                        if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                            return CheckQuestItems(env, var, var + 1, false, 10000, 10001, 182215709, 1);
                    }
                    if (var == 1)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1352);

                        if (dialogActionId == DialogAction.SETPRO2)
                            return DefaultCloseDialog(env, var, var + 1, 182215711, 1);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            switch (targetId)
            {
                case 804910: // Varla
                    if (dialogActionId == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);

                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return HandlerResult.UNKNOWN;

        int id = item.GetItemTemplate().GetTemplateId();
        if (id != 182215711)
            return HandlerResult.UNKNOWN;

        int itemObjId = item.GetObjectId();
        PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 1000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            int var = qs.GetQuestVarById(0);
            if (var == 2)
            {
                SpawnTemporarily(805161, player.GetWorldMapInstance(), player.GetX() + 2, player.GetY() + 2, player.GetZ(), (byte)60, 2);
                qs.SetStatus(QuestStatus.REWARD);
                qs.SetQuestVar(var + 1);
                UpdateQuestStatus(env);
            }
            return ValueTask.CompletedTask;
        }, 1000L);
        return HandlerResult.SUCCESS;
    }
}
