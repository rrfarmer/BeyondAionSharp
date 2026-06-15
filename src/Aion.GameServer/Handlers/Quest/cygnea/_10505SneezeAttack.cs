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

/// <summary>
/// @Author Majka
/// </summary>
public class _10505SneezeAttack : AbstractQuestHandler
{
    private static readonly int workItemId = 182215612; // Restored Beritra's Plans

    public _10505SneezeAttack() : base(10505)
    {
    }

    public override void Register()
    {
        // Balaur Maneuver Plans 702672
        // Averse 804707
        // Erinos 804708
        int[] npcs = { 702672, 804707, 804708 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        int[] items = { 182215609, 182215610, 182215611, 182215612 };
        foreach (int item in items)
            qe.RegisterQuestItem(item, questId);
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
            case 804707: // Averse
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 0) // Step 0: Talk with Averse at Erivale Territory Village.
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
            case 804708: // Erinos
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 1) // Step 1: Talk with Investigator Erinos who has been dispatched to Twilight Temple.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 1352);
                        }

                        if (dialogActionId == DialogAction.SETPRO2)
                        {
                            return DefaultCloseDialog(env, var, var + 1);
                        }
                    }

                    if (var == 2) // Step 2: Gather the Balaur Maneuver Plans and talk with Erinos. (Item check)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 1693);
                        }

                        if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                        {
                            if (QuestService.CollectItemCheck(env, true))
                            {
                                qs.SetQuestVar(var + 1);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 10000);
                            }
                            return SendQuestDialog(env, 10001);
                        }
                    }

                    if (var == 3) // Step 3: Talk with Erinos.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 2034);
                        }

                        if (dialogActionId == DialogAction.SETPRO4)
                        {
                            return DefaultCloseDialog(env, var, var + 1, workItemId, 1);
                        }
                    }
                }
                break;
            case 702672:
                if (dialogActionId == DialogAction.USE_OBJECT && var == 2) // Step 2: Gather the Balaur Maneuver Plans and talk with Erinos.
                {
                    return UseQuestObject(env, var, var, false, 0);
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
        PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 1000, 0, 0), true);
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.BroadcastPacket(player, new SmItemUsageAnimation(player.GetObjectId(), itemObjId, id, 0, 1, 0), true);
            int var = qs.GetQuestVarById(0);
            if (var == 4) // Step 4: Read the Deciphered Balaur Document and find out what it says.
            {
                PlayQuestMovie(env, 992);
                qs.SetQuestVar(var + 1);
                qs.SetStatus(QuestStatus.REWARD);
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
