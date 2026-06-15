using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Balthazar, fixed Shaman, vlog
/// </summary>
public class _1636AFluteForTheFixing : AbstractQuestHandler
{
    public _1636AFluteForTheFixing() : base(1636)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204535).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204535).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700239).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(203792).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 204535) // Maximus
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
                case 203792: // Utsida
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            else if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            else if (var == 2)
                            {
                                return SendQuestDialog(env, 2034);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            return CheckQuestItems(env, 1, 2, false, 10000, 10001); // 2
                        case DialogAction.SETPRO4:
                            return DefaultCloseDialog(env, 2, 3, 182201785, 1, 182201789, 1); // 3
                        case DialogAction.FINISH_DIALOG:
                            return SendQuestSelectionDialog(env);
                    }
                    break;
                case 700239: // Drake Stone Statue
                    return PlayQuestMovie(env, 210);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 204535) // Maximus
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

    public override bool OnCanAct(QuestEnv env, QuestActionType questEventType, params object[] objects)
    {
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(env.GetQuestId());
        return env.GetTargetId() == 700239 && qs != null && qs.GetStatus() == QuestStatus.START && qs.GetQuestVarById(0) == 3;
    }

    public override void OnMovieEndEvent(QuestEnv env, int movieId)
    {
        if (movieId == 210)
        {
            ChangeQuestStep(env, 3, 3, true);
            VisibleObject drakeStoneStatue = env.GetVisibleObject();
            drakeStoneStatue.GetController().DeleteAndScheduleRespawn();
            SpawnTemporarily(212008, drakeStoneStatue.GetWorldMapInstance(), drakeStoneStatue.GetX(), drakeStoneStatue.GetY(), drakeStoneStatue.GetZ(), (byte)30, 60);
        }
    }
}
