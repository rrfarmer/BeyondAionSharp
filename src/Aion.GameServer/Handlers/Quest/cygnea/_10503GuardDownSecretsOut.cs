using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _10503GuardDownSecretsOut : AbstractQuestHandler
{
    private static readonly int mobTargetId = 236252; // Aetheric Field Guard
    private static readonly int collectItemId = 182215603; // Old Balaur Document
    private static readonly int workItemId = 182215604; // Deciphered Balaur Document

    public _10503GuardDownSecretsOut() : base(10503)
    {
    }

    public override void Register()
    {
        // Eukraton 804704
        // Euvia 804705
        // Scattered Balaur Document 702670
        int[] npcs = { 702670, 804704, 804705 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(mobTargetId).AddOnKillEvent(questId);
        qe.RegisterQuestItem(collectItemId, questId);
        qe.RegisterQuestItem(workItemId, questId);
        qe.RegisterOnEnterZone(ZoneName.Get("LF5_SENSORYAREA_Q10503_206364_3_210070000"), questId); // Aetheric Field Stone of Earth zone
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
            case 804704: // Eukraton
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 0) // Step 0: Talk with Eukraton at Aequis Advance Post.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 1011);
                        }

                        if (dialogActionId == DialogAction.SETPRO1)
                        {
                            return DefaultCloseDialog(env, var, var + 1);
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
            case 804705: // Euvia
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 2) // Step 2: Obtain the Old Balaur Document from the Scattered Balaur Document and take it to Euvia
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 1693);
                        }

                        if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                        {
                            long itemCount = player.GetInventory().GetItemCountByItemId(collectItemId);
                            if (itemCount > 0)
                            {
                                RemoveQuestItem(env, collectItemId, itemCount);
                                qs.SetQuestVar(var + 1);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 10000);
                            }
                            else
                            {
                                return SendQuestDialog(env, 10001);
                            }
                        }
                    }

                    if (var == 3) // Step 3: Talk with Euvia at Dragon Lord's Gardens.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 2034);
                        }

                        if (dialogActionId == DialogAction.SETPRO4)
                        {
                            GiveQuestItem(env, workItemId, 1);
                            return DefaultCloseDialog(env, var, var + 1);
                        }
                    }

                    if (var == 5) // Step 5: Talk with Euvia at Dragon Lord's Gardens.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 2716);
                        }

                        if (dialogActionId == DialogAction.SETPRO6)
                        {
                            return DefaultCloseDialog(env, var, var + 1);
                        }
                    }
                }
                break;
            case 702670:
                if (dialogActionId == DialogAction.USE_OBJECT && var == 2) // Step 2: Obtain the Old Balaur Document from the Scattered Balaur Document and take it to Euvia.
                {
                    return UseQuestObject(env, 2, 2, false, 0);
                }
                break;
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName) // Step 6: Examine the Aetheric Field Stone of Earth.
    {
        if (zoneName == ZoneName.Get("LF5_SENSORYAREA_Q10503_206364_3_210070000"))
        {
            Player player = env.GetPlayer();
            if (player == null)
            {
                return false;
            }

            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);

                if (var == 6)
                {
                    qs.SetQuestVar(var + 1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    PlayQuestMovie(env, 991);
                    player.GetMoveController().AbortMove();
                }
            }
            return true;
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        int targetId = env.GetTargetId();

        if (var == 1) // Step 1: Eliminate Aetheric Field Guards (3) at Dragon Lord's Gardens.
        {
            if (DefaultOnKillEvent(env, targetId, 0, 3, 1)) // 1-2 with varNum 1
            {
                int varKill = qs.GetQuestVarById(1); // Kill counter
                if (varKill >= 3)
                {
                    qs.SetQuestVar(2);
                    UpdateQuestStatus(env);
                }
                return true;
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
        if (id != workItemId)
            return HandlerResult.UNKNOWN;

        int itemObjId = item.GetObjectId();
        PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 1000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            int var = qs.GetQuestVarById(0);
            if (var == 4) // Step 4: Read the Deciphered Balaur Document and find out what it says.
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
