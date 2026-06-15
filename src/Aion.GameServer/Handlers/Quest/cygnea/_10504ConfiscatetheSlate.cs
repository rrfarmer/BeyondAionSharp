using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _10504ConfiscatetheSlate : AbstractQuestHandler
{
    private static readonly int workItem = 182215606; // Flame Pepper Bomb
    private static readonly int collectItemId = 182215607; // Broken Slate Piece

    public _10504ConfiscatetheSlate() : base(10504)
    {
    }

    public override void Register()
    {
        // Cracked Oath Slate 702671
        // Atrape 804706
        int[] npcs = { 702671, 804706 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        int[] mobs = { 236253, 236254, 236255 }; // Beritra Troopers, Beritra Sentinels, Commander Nazracht
        foreach (int mob in mobs)
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        qe.RegisterQuestItem(collectItemId, questId);
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
            case 804706: // Atrape
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 0) // Step 0: Talk with Atrape at Andrea Territory Village.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 1011);
                        }

                        if (dialogActionId == DialogAction.SETPRO1)
                        {
                            return DefaultCloseDialog(env, 0, 1, workItem, 1); // 1
                        }
                    }

                    if (var == 3) // Step 2: Obtain the Old Balaur Document from the Scattered Balaur Document and take it to Euvia
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 2034);
                        }

                        if (dialogActionId == DialogAction.CHECK_USER_HAS_QUEST_ITEM)
                        {
                            long itemCount = player.GetInventory().GetItemCountByItemId(collectItemId);
                            if (itemCount > 0)
                            {
                                RemoveQuestItem(env, collectItemId, itemCount);
                                qs.SetQuestVar(var + 1);
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
            case 702671:
                if (dialogActionId == DialogAction.USE_OBJECT && var == 3) // Step 3: Obtain the Broken Slate Piece from the Cracked Oath Slate and take it to Atrape.
                {
                    return UseQuestObject(env, 3, 3, false, 0);
                }
                break;
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

        // Step 1: Eliminate the Beritra Troopers and Beritra Sentinels (5).
        // Step 2: Use the Flame Pepper Bomb to torture and obtain information from Commander Nazracht (1).
        switch (targetId)
        {
            case 236253:
            case 236254:
                if (var == 1)
                {
                    int varKill = qs.GetQuestVarById(1); // Kill counter
                    qs.SetQuestVarById(1, varKill + 1); // 1-5 with varNum 1
                    if (varKill >= 4)
                    {
                        qs.SetQuestVar(var + 1);
                    }
                    UpdateQuestStatus(env);
                    return true;
                }
                break;
            case 236255:
                if (var == 2)
                {
                    qs.SetQuestVar(var + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                break;
        }
        return false;
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
