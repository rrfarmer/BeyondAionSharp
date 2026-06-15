using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr. Poke, Gigi, vlog
/// </summary>
public class _2223AMythicalMonster : AbstractQuestHandler
{
    public _2223AMythicalMonster() : base(2223)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203616).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(203616).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700134).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(211621).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203616) // Gefion
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 1011);
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
                case 203620: // Lamir
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            else if (var == 1)
                            {
                                if (player.GetInventory().GetItemCountByItemId(182203217) == 1)
                                {
                                    return SendQuestDialog(env, 1694);
                                }
                                else
                                {
                                    GiveQuestItem(env, 182203217, 1);
                                    return SendQuestDialog(env, 1779);
                                }
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1, 182203217, 1, 0, 0); // 1
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
                case 700134: // Old Incense Burner
                    if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
                    {
                        if (player.GetInventory().GetItemCountByItemId(182203217) == 1)
                        {
                            return UseQuestObject(env, 1, 1, false, 0, 0, 0, 182203217, 1, 67, true); // movie + die
                        }
                    }
                    break;
                case 203616: // Gefion
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 2716);
                            }
                            return false;
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 203616) // Gefion
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        return SendQuestDialog(env, 5);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        return DefaultOnKillEvent(env, 211621, 1, true); // reward
    }

    public override void OnMovieEndEvent(QuestEnv env, int movieId)
    {
        if (movieId == 67)
            SpawnForFiveMinutes(211621, env.GetPlayer().GetWorldMapInstance(), (float)1547.1047, (float)894.2969, (float)248.019, (byte)85);
    }
}
