using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _10502CygneaSecrets : AbstractQuestHandler
{
    private static readonly int workItemId = 182215601; // Rubbing Tool Bag

    public _10502CygneaSecrets() : base(10502)
    {
    }

    public override void Register()
    {
        // Nixkon 804701
        // Nixkon in Hiding 804702
        // Shining Lump of Soil 702669
        // Ancient Fossil 731537
        int[] npcs = { 702669, 731537, 804701, 804702 };
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
            case 804701: // Nixkon
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 0) // Step 0: Talk with Nixkon at the Henor Territory Village.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1011);

                        if (dialogActionId == DialogAction.SETPRO1)
                            return DefaultCloseDialog(env, var, var + 1);
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
            case 804702: // Nixkon in Hiding
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 1) // Step 1: Look for Nixkon in Hiding in the Black Fin Village.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1352);

                        if (dialogActionId == DialogAction.SETPRO2)
                            return DefaultCloseDialog(env, var, var + 1);
                    }

                    if (var == 2) // Step 2: Collect the Shining Soil Sample from the Shining Lump of Soil and report back to Nixkon in Hiding.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1693);

                        if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                        {
                            long itemCount = player.GetInventory().GetItemCountByItemId(182215600);
                            if (itemCount >= 3)
                            {
                                RemoveQuestItem(env, 182215600, itemCount);
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

                    if (var == 3) // Step 3: Talk with Nixkon in Hiding at the Black Fin Village.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2034);
                        if (dialogActionId == DialogAction.SETPRO4)
                            return DefaultCloseDialog(env, var, var + 1, workItemId, 1);
                    }

                    if (var == 5) // Step 5: Talk with Nixkon in Hiding at the Black Fin Village.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2716);
                        if (dialogActionId == DialogAction.SETPRO6)
                        {
                            long itemCount = player.GetInventory().GetItemCountByItemId(182215602);
                            if (itemCount > 0)
                            {
                                RemoveQuestItem(env, 182215602, itemCount);
                            }
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
            case 702669:
                if (dialogActionId == DialogAction.USE_OBJECT && var == 2)
                {
                    return UseQuestObject(env, 2, 2, false, 0);
                }
                break;
            case 731537:
                if (var == 6) // Step 6: Examine the Ancient Creature Fossil.
                {
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 3057);
                    if (dialogActionId == DialogAction.SET_SUCCEED)
                    {
                        GiveQuestItem(env, 182215635, 1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return DefaultCloseDialog(env, var, var + 1);
                    }
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
            return HandlerResult.FAILED;

        int id = item.GetItemTemplate().GetTemplateId();
        if (id != workItemId)
            return HandlerResult.FAILED;

        int var = qs.GetQuestVarById(0);
        if (var == 4) // Step 4: Use the Rubbing Tool Bag on the Unusual Mural in the Black Fin Village.
        {
            GiveQuestItem(env, 182215602, 1);
            return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, var, var + 1, false));
        }

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
