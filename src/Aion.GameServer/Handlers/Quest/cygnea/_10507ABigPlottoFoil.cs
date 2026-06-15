using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @Author Majka
/// </summary>
public class _10507ABigPlottoFoil : AbstractQuestHandler
{
    public _10507ABigPlottoFoil() : base(10507)
    {
    }

    public override void Register()
    {
        // Aithria 804711
        // Pluvis 804712
        // Pruina 804713
        // Ricoro 804714
        // Nostibas 804715
        int[] npcs = { 804711, 804712, 804713, 804714, 804715 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);

        // Beritra Guard 236264 236265
        // Beritra Supplies Box 702668
        int[] mobs = { 236264, 236265, 702668 };
        foreach (int mob in mobs)
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        qe.RegisterOnEnterZone(ZoneName.Get("LF5_SENSORYAREA_Q10507_206366_2_210070000"), questId); // Beritra Guard Captain's Tent zone
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterOnQuestCompleted(questId);
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
            case 804711: // Aithria
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 0) // Step 0: Talk with Aithria at Aequis Detachment Post.
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

                    if (var == 4) // Step 4: Talk with Aithria at Aequis Detachment Post.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 2375);
                        }

                        if (dialogActionId == DialogAction.SETPRO5)
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
            case 804712: // Pluvis
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 1) // Step 1: Talk with Pluvis at Aequis Detachment Post.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 1352);
                        }

                        if (dialogActionId == DialogAction.SETPRO2)
                        {
                            return DefaultCloseDialog(env, var, var + 1); // 1
                        }
                    }
                }
                break;
            case 804713: // Pruina
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 2) // Step 2: Talk with Pruina at Aequis Detachment Post.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 1693);
                        }

                        if (dialogActionId == DialogAction.SETPRO3)
                        {
                            return DefaultCloseDialog(env, var, var + 1); // 1
                        }
                    }
                }
                break;
            case 804714: // Ricoro
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 3) // Step 3: Talk with Ricoro at Aequis Detachment Post.
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 2034);
                        }

                        if (dialogActionId == DialogAction.SETPRO4)
                        {
                            return DefaultCloseDialog(env, var, var + 1); // 1
                        }
                    }
                }
                break;
            case 804715: // Nostibas
                if (qs.GetStatus() == QuestStatus.START)
                {
                    if (var == 5) // Step 5:
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                        {
                            return SendQuestDialog(env, 2716);
                        }

                        if (dialogActionId == DialogAction.SETPRO6)
                        {
                            return DefaultCloseDialog(env, var, var + 1); // 1
                        }
                    }
                }
                break;
        }
        return false;
    }

    public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName) // Step 7: Infiltrate Beritra Guard Captain's Tent.
    {
        if (zoneName == ZoneName.Get("LF5_SENSORYAREA_Q10507_206366_2_210070000"))
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

                if (var == 7)
                {
                    qs.SetQuestVar(var + 1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    PlayQuestMovie(env, 993);
                    player.GetMoveController().AbortMove();
                }
            }
            return true;
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

        if (var == 6) // Step 6: Eliminate Beritra Guards (5). Destroy Beritra Supplies Boxes (3)
        {
            int varKill1 = qs.GetQuestVarById(1); // Kill counter var 1
            int varKill2 = qs.GetQuestVarById(2); // Kill counter var 2
            if (targetId == 702668 && varKill2 <= 2) // Beritra Supplies Box
            {
                qs.SetQuestVarById(2, varKill2 + 1); // 1-3 with varNum 2
                UpdateQuestStatus(env);
            }
            else if ((targetId == 236264 || targetId == 236265) && varKill1 <= 4) // Beritra Guards
            {
                qs.SetQuestVarById(1, varKill1 + 1); // 1-5 with varNum 1
                UpdateQuestStatus(env);
            }

            if (varKill1 >= 4 && varKill2 >= 2)
            {
                qs.SetQuestVar(var + 1);
                UpdateQuestStatus(env);
            }
            return true;
        }
        return false;
    }

    public override void OnMovieEndEvent(QuestEnv env, int movieId)
    {
        if (movieId != 993)
            return;
        Player player = env.GetPlayer();
        SpawnTemporarily(236266, player.GetWorldMapInstance(), player.GetX() + 2, player.GetY() + 3, player.GetZ(), (byte)73, 2);
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
