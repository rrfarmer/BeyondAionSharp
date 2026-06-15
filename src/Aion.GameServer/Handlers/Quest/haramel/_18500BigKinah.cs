using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi, vlog, Majka
/// </summary>
public class _18500BigKinah : AbstractQuestHandler
{
    public _18500BigKinah() : base(18500)
    {
    }

    public override void Register()
    {
        int[] npcs = { 203106, 203166, 730304, 730305, 799522, 206150 };
        qe.RegisterQuestNpc(203106).AddOnQuestStart(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("LF1A_SENSORYAREA_Q18500_206150_3_210030000"), questId); // Haramel Entrance Zone
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 203106) // Alisdair
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
                case 203166: // Zephyros
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                    break;
                case 730304: // Suspicious Odium Piece
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            return false;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, 1, 2); // 2
                    }
                    break;
                case 730305: // Suspicious Odium Pile
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            return false;
                        case DialogAction.SETPRO3:
                            return DefaultCloseDialog(env, 2, 3); // 3
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799522) // Moorilerk
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                {
                    return SendQuestDialog(env, 5);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName) // Investigate Suspicious Relic Site nearby.
    {
        if (zoneName == ZoneName.Get("LF1A_SENSORYAREA_Q18500_206150_3_210030000"))
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

                if (var == 3)
                {
                    PlayQuestMovie(env, 175);
                    player.GetMoveController().AbortMove();
                }
            }
            return true;
        }
        return false;
    }

    public override void OnMovieEndEvent(QuestEnv env, int movieId)
    {
        if (movieId == 175)
            ChangeQuestStep(env, 3, 3, true); // reward
    }
}
