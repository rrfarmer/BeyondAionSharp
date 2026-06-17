using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author kecimis
/// </summary>
public class _3200PriceOfGoodwill : AbstractQuestHandler
{
    private static readonly int[] npc_ids = { 204658, 798332, 700522, 279006, 798322 };

    /*
     * 204658 - Roikinerk 798332 - Haorunerk 700522 - Haorunerks Bag 279006 - Garkbinerk 798322 - Kuruminerk
     */

    public _3200PriceOfGoodwill() : base(3200)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204658).AddOnQuestStart(questId); // Roikinerk
        qe.RegisterQuestItem(182209082, questId); // Teleport Scroll
        foreach (int npc_id in npc_ids)
            qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
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
            if (targetId == 204658) // Roikinerk
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
            return false;
        }

        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 798322) // Kuruminerk
            {
                if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    return SendQuestDialog(env, 10002);
                else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
                    return SendQuestDialog(env, 5);
                else
                    return SendQuestEndDialog(env);
            }
            return false;
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 204658 && var == 0)
            { // Roikinerk
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1003);
                    case DialogAction.SETPRO1:
                        WorldMapInstance steelRake = InstanceService.GetNextAvailableInstance(WorldMapType.STEEL_RAKE.GetId(), player);
                        Npc haorunerksCorpse = steelRake.GetNpc(798333);
                        Spawn(798332, steelRake, haorunerksCorpse.GetX(), haorunerksCorpse.GetY(), haorunerksCorpse.GetZ(), (byte)30);
                        haorunerksCorpse.GetController().Delete();
                        TeleportService.TeleportTo(player, steelRake, 403.55f, 508.11f, 885.77f);
                        return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 798332 && var == 1)
            { // Haorunerk
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SELECT2_1:
                        PlayQuestMovie(env, 431);
                        break;
                    case DialogAction.SETPRO2:
                        return DefaultCloseDialog(env, 1, 2);
                }
            }
            else if (targetId == 700522 && var == 2)
            { // Haorunerks Bag, loc: 401.24 503.19 885.76 119
                ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    UpdateQuestStatus(env);
                    return ValueTask.CompletedTask;
                }, 3000L);
                return true;
            }
            else if (targetId == 279006 && var == 3)
            { // Garkbinerk
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2034);
                    case DialogAction.SET_SUCCEED:
                        return DefaultCloseDialog(env, 3, 3, true, false);
                }
            }
        }
        return false;
    }

    public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
    {
        Player player = env.GetPlayer();
        int id = item.GetItemTemplate().GetTemplateId();
        int itemObjId = item.GetObjectId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (id != 182209082 || qs == null || qs.GetQuestVarById(0) != 2)
            return HandlerResult.UNKNOWN;

        PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 3000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            RemoveQuestItem(env, 182209082, 1);
            // teleport location(BlackCloudIsland): 400010000 3419.16 2445.43 2766.54 57
            TeleportService.TeleportTo(player, 400010000, 3419.16f, 2445.43f, 2766.54f, (byte)57);
            qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
            UpdateQuestStatus(env);
            return ValueTask.CompletedTask;
        }, 3000L);
        return HandlerResult.SUCCESS;
    }
}
