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
/// @Author Majka
/// </summary>
public class _10501ResearchtheRuins : AbstractQuestHandler
{
    private static readonly int workItemId = 182215598; // Ruins Location Map

    public _10501ResearchtheRuins() : base(10501)
    {
    }

    public override void Register()
    {
        // Mosphera 804700
        // Ancient Balaur Corpse 731535
        // Collapsed Ruins 731536
        int[] npcs = { 804700, 731535, 731536 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        qe.RegisterQuestItem(workItemId, questId);
        qe.RegisterOnQuestCompleted(questId);
        qe.RegisterOnLevelChanged(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        switch (targetId)
        {
            case 804700: // Mosphera
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 0) // Step 0: Talk with Mosphera at the Aequis First Hold.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1011);

                        if (dialogActionId == DialogAction.SETPRO1)
                            return DefaultCloseDialog(env, var, var + 1, workItemId, 1); // Gives item Ruins Location Map [ID: 182215598]
                    }

                    if (var == 6) // Step 6: Obtain the Token of Balaur and take it to Mosphera.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 3057);

                        // @ToCheck: is Cygnea Aetheric Field Stone [ID: 702760] related with mission?
                        if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                        {
                            long itemCount = player.GetInventory().GetItemCountByItemId(182215599);
                            if (itemCount > 0)
                            {
                                RemoveQuestItem(env, 182215599, itemCount);
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 10000);
                            }
                            else
                            {
                                return SendQuestDialog(env, 10001);
                            }
                        }
                    }
                }
                if (qs.GetStatus() == QuestStatus.REWARD)
                {
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        return SendQuestDialog(env, 10002);
                    }

                    return SendQuestEndDialog(env);
                }
                break;
            case 731535: // Ancient Balaur Corpse
                if (var == 4) // Step 4: Examine the Ancient Balaur Corpse.
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 2375);
                    if (dialogActionId == DialogAction.SETPRO5)
                    {
                        // Spawn Beritra Raider for 10 minutes
                        SpawnTemporarily(236250, player.GetWorldMapInstance(), 2067.6863f, 386.52222f, 565.7099f, (byte)70, 10);
                        return DefaultCloseDialog(env, var, var + 1);
                    }
                }
                break;
            case 731536: // Collapsed Ruins
                if (var == 2) // Step 2: Examine the Collapsed Ruins
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 1693);
                    if (dialogActionId == DialogAction.SETPRO3)
                        return DefaultCloseDialog(env, var, var + 1);
                }
                break;
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
        if (id != workItemId)
            return HandlerResult.UNKNOWN;

        int itemObjId = item.GetObjectId();
        PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 1000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            int var = qs.GetQuestVarById(0);
            // Step 1: Use the Ruins Location Map to find the first location
            // Step 3: Use the Ruins Location Map to find the second location
            // Step 5: Use the Ruins Location Map to find the third location
            if (var == 1 || var == 3 || var == 5)
            {
                qs.SetQuestVar(var + 1);
                UpdateQuestStatus(env);
            }
            return ValueTask.CompletedTask;
        }, 1000L);
        return HandlerResult.SUCCESS;
    }

    public override void OnQuestCompletedEvent(QuestEnv env)
    {
        DefaultOnQuestCompletedEvent(env, 10500);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player, 10500);
    }
}
