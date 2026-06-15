using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi, vlog, Majka
/// </summary>
public class _28510DestroytheHaramelFacilities : AbstractQuestHandler
{
    public _28510DestroytheHaramelFacilities() : base(28510)
    {
    }

    public override void Register()
    {
        int[] npcs = { 804605, 700953, 203560 };
        qe.RegisterQuestNpc(804605).AddOnQuestStart(questId);
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
        qe.RegisterQuestNpc(700950).AddOnKillEvent(questId);
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        return DefaultOnKillEvent(env, 700950, 0, 3); // Aether Carts: 1, 2, 3
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 804605) // Shezen
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else
                {
                    return SendQuestStartDialog(env, 182212021, 1);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);
            switch (targetId)
            {
                case 700953: // Processed Odella
                    if (dialogActionId == DialogAction.USE_OBJECT)
                    {
                        if (var >= 3 && var < 5)
                        {
                            return UseQuestObject(env, var, var + 1, false, true); // 4,5
                        }
                        else if (var == 5)
                        {
                            return UseQuestObject(env, var, var + 1, true, true); // Reward
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203560) // Morn
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 10002);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }
}
