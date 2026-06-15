using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author vlog
/// </summary>
public class _18511OutOfThePast : AbstractQuestHandler
{
    public _18511OutOfThePast() : base(18511)
    {
    }

    public override void Register()
    {
        int[] npcs = { 799522, 700954, 730359, 790000 };
        qe.RegisterQuestNpc(799522).AddOnQuestStart(questId);
        qe.RegisterOnGetItem(182212011, questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799522)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 700954: // Well-Dried Ginseng
                {
                    return true; // loot
                }
                case 730359: // Huge Cauldron
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 0, 0, false, 1352, 10001);
                        case DialogAction.SETPRO2:
                            GiveQuestItem(env, 182212011, 1);
                            return CloseDialogWindow(env);
                        case DialogAction.FINISH_DIALOG:
                            return CloseDialogWindow(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 790000)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnGetItemEvent(QuestEnv env)
    {
        DefaultOnGetItemEvent(env, 0, 1, false); // 1
        ChangeQuestStep(env, 1, 1, true); // reward
        return true;
    }
}
