using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi, Majka
/// </summary>
public class _28500OdellaOdellaWhereArtThou : AbstractQuestHandler
{
    public _28500OdellaOdellaWhereArtThou() : base(28500)
    {
    }

    public override void Register()
    {
        int[] npcs = { 203560, 203649, 730306, 730307, 799522 };
        qe.RegisterQuestNpc(203560).AddOnQuestStart(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("DF1A_SENSORYAREA_Q28500_206151_3_220030000"), questId);
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
            if (targetId == 203560) // Morn
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
                case 203649: // Gulkalla
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
                case 730306: // Discarded Odium Piece
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
                case 730307: // Discarded Odium Pile
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            return false;
                        case DialogAction.SETPRO3:
                            PlayQuestMovie(env, 217);
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

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName) // Investigate the Abandoned Relic Site.
    {
        if (zoneName == ZoneName.Get("DF1A_SENSORYAREA_Q28500_206151_3_220030000"))
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
                    PlayQuestMovie(env, 217);
                    player.GetMoveController().AbortMove();
                }
            }
            return true;
        }
        return false;
    }

    public override void OnMovieEndEvent(QuestEnv env, int movieId)
    {
        if (movieId == 217)
            ChangeQuestStep(env, 3, 3, true); // reward
    }
}
