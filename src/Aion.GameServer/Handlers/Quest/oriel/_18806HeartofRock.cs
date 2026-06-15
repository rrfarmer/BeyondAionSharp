using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi
/// </summary>
public class _18806HeartofRock : AbstractQuestHandler
{
    private static readonly HashSet<int> butlers;

    static _18806HeartofRock()
    {
        butlers = new HashSet<int>();
        butlers.Add(810017);
        butlers.Add(810018);
        butlers.Add(810019);
        butlers.Add(810020);
        butlers.Add(810021);
    }

    public _18806HeartofRock() : base(18806)
    {
    }

    public override void Register()
    {
        foreach (int butlerId in butlers)
        {
            qe.RegisterQuestNpc(butlerId).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(butlerId).AddOnTalkEvent(questId);
        }
        qe.RegisterQuestNpc(830528).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830194).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (butlers.Contains(targetId))
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.QUEST_ACCEPT_1:
                    case DialogAction.QUEST_ACCEPT_SIMPLE:
                        return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 830528:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                return SendQuestDialog(env, 1352);
                            }
                        case DialogAction.SELECT2_1:
                            {
                                return SendQuestDialog(env, 1353);
                            }
                        case DialogAction.SETPRO1:
                            {
                                return DefaultCloseDialog(env, 0, 1);
                            }
                    }
                    break;
                case 830194:
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            {
                                return SendQuestDialog(env, 2375);
                            }
                        case DialogAction.SELECT_QUEST_REWARD:
                            ChangeQuestStep(env, 1, 1, true);
                            return SendQuestDialog(env, 5);
                    }
                    break;
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 830194)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
