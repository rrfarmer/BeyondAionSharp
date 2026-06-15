using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _39520VilmanerkVersusDragonbound : AbstractQuestHandler
{
    private int[] mobs = { 218387, 218389, 218391, 218393, 218395, 218397, 218399, 218401 };

    public _39520VilmanerkVersusDragonbound() : base(39520)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(205885).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(205984).AddOnTalkEvent(questId);
        foreach (int mob in mobs)
        {
            qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
        }
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int targetId = env.GetTargetId();
        int dialogActionId = env.GetDialogActionId();

        if (targetId == 0)
        {
            switch (dialogActionId)
            {
                case DialogAction.QUEST_ACCEPT_1:
                    QuestService.StartQuest(env);
                    return CloseDialogWindow(env);
                default:
                    return CloseDialogWindow(env);
            }
        }

        if (qs == null)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 205984)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SETPRO1:
                        env.GetVisibleObject().GetController().Delete();
                        return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (targetId == 205885)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SELECT_QUEST_REWARD:
                        ChangeQuestStep(env, 1, 1, true);
                        return SendQuestDialog(env, 5);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 205885)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 2375);
                }
                else
                {
                    return SendQuestEndDialog(env);
                }
            }
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (Rnd.Chance() < 20)
            {
                Npc npc = (Npc)env.GetVisibleObject();
                npc.GetController().Delete();
                SpawnForFiveMinutes(205984, npc.GetPosition());
                return true;
            }
        }
        return false;
    }
}
