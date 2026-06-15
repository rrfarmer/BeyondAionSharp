using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Quest Starter: Hresvelgr (204837). Go to Dark Poeta and find the Balaur Operation Order (730192). Destroy the Telepathy Controller (214894) (1).
/// Destroy power generators to close the Balaur Abyss Gate: Main Power Generator (214895) (1), Auxiliary Power Generator (214896) (1), Emergency
/// Generator (214897) (1). Get rid of Brigade General Anuhart (214904), and take the Concentrated Vitality (182204534) to Hresvelgr (204837).
/// @author vlog
/// </summary>
public class _4502EssenceOfFate : AbstractQuestHandler
{
    private static readonly int[] npcs = { 204837, 730192 };
    private static readonly int[] mobs = { 214894, 214895, 214896, 214897, 214904 };

    public _4502EssenceOfFate() : base(4502)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204837).AddOnQuestStart(questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
        foreach (int mob in mobs)
        {
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        Npc npc = (Npc)env.GetVisibleObject();
        int targetId = npc.GetNpcId();
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204837)
            { // Hresvelgr
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
                case 730192: // Balaur Operation Orders
                    if (var == 0)
                    {
                        if (dialogActionId == DialogAction.USE_OBJECT)
                        {
                            return SendQuestDialog(env, 1011);
                        }
                        else
                        {
                            ChangeQuestStep(env, 0, 1); // 1
                            return SendQuestDialog(env, 0);
                        }
                    }
                    break;
                case 204837: // Hresvelgr
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 2, 2, true, 5, 10001); // reward
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204837)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        Npc npc = (Npc)env.GetVisibleObject();
        int targetId = npc.GetNpcId();
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            int var1 = qs.GetQuestVarById(1);
            int var2 = qs.GetQuestVarById(2);
            int var3 = qs.GetQuestVarById(3);

            switch (targetId)
            {
                case 214894: // Telepathy Controller
                    if (var == 1)
                        return DefaultOnKillEvent(env, 214894, 1, 2, 0); // 2
                    break;
                case 214895: // Main Power Generator
                    if (var == 2 && var1 != 1)
                    {
                        DefaultOnKillEvent(env, 214895, 0, 1, 1); // 1: 1
                        return true;
                    }
                    break;
                case 214896: // Auxiliary Power Generator
                    if (var == 2 && var2 != 1)
                    {
                        DefaultOnKillEvent(env, 214896, 0, 1, 2); // 2: 1
                        return true;
                    }
                    break;
                case 214897: // Emergency Generator
                    if (var == 2 && var3 != 1)
                    {
                        DefaultOnKillEvent(env, 214897, 0, 1, 3); // 3: 1
                        return true;
                    }
                    break;
            }
        }
        return false;
    }
}
