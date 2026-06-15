using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Majka
/// </summary>
public class _25051TreasureOfAncientKings : AbstractQuestHandler
{
    public _25051TreasureOfAncientKings() : base(25051)
    {
    }

    public override void Register()
    {
        // Recluse's grave 731553
        // Recluse's relic 731554
        // Ancient King's treasure chest 731555
        // Soglo 804915
        // Rolef 804916
        // Zagmus 805160
        qe.RegisterQuestNpc(804915).AddOnQuestStart(questId);
        int[] npcs = { 731553, 731554, 731555, 804915, 804916, 805160 };
        foreach (int npc in npcs)
        {
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        }
        qe.RegisterOnInvisibleTimerEnd(questId);
        qe.RegisterOnLogOut(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 804915) // Soglo
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 4762);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            int var = qs.GetQuestVarById(0);

            switch (targetId)
            {
                case 731553: // Recluse's grave
                    if (var == 0)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1011);

                        if (dialogActionId == DialogAction.SETPRO1)
                        {
                            // Spawn of Zagmus
                            SpawnForFiveMinutes(805160, env.GetVisibleObject().GetWorldMapInstance(), 2046.8f, 1588.8f, 348.4f, (byte)90);
                            QuestService.InvisibleTimerStart(env, 300);
                            return DefaultCloseDialog(env, var, var + 1);
                        }
                    }
                    break;
                case 805160: // Zagmus
                    if (var == 1)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1352);

                        if (dialogActionId == DialogAction.SETPRO2)
                            return DefaultCloseDialog(env, var, var + 1);
                    }
                    break;
                case 731554: // Recluse's relic
                    if (var == 2)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 1693);

                        if (dialogActionId == DialogAction.SETPRO3)
                            return DefaultCloseDialog(env, var, var + 1, 182215720, 1);
                    }
                    break;
                case 731555: // Ancient King's treasure chest
                    if (var == 3)
                    {
                        if (dialogActionId == DialogAction.QUEST_SELECT)
                            return SendQuestDialog(env, 2034);

                        if (dialogActionId == DialogAction.SET_SUCCEED)
                        {
                            Npc npc = (Npc)env.GetVisibleObject();
                            SpawnForFiveMinutes(220031, npc.GetWorldMapInstance(), npc.GetX() - 2, npc.GetY() + 2, npc.GetZ(), (byte)10);
                            RemoveQuestItem(env, 182215720, 1);
                            qs.SetQuestVar(var + 1);
                            return DefaultCloseDialog(env, var + 1, var + 1, true, false);
                        }
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            switch (targetId)
            {
                case 804916: // Rolef
                    if (dialogActionId == DialogAction.USE_OBJECT)
                        return SendQuestDialog(env, 10002);

                    return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnLogOutEvent(QuestEnv env)
    {
        return RestoreQuestStep(env);
    }

    public override bool OnInvisibleTimerEndEvent(QuestEnv env)
    {
        return RestoreQuestStep(env);
    }

    private bool RestoreQuestStep(QuestEnv env)
    {
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);

        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        if (var == 1)
        {
            qs.SetQuestVar(0);
            UpdateQuestStatus(env);
        }
        return true;
    }
}
