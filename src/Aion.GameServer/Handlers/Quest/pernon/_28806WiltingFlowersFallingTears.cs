using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi
/// </summary>
public class _28806WiltingFlowersFallingTears : AbstractQuestHandler
{
    private static readonly HashSet<int> butlers;

    static _28806WiltingFlowersFallingTears()
    {
        butlers = new HashSet<int>();
        butlers.Add(810022);
        butlers.Add(810023);
        butlers.Add(810024);
        butlers.Add(810025);
        butlers.Add(810026);
    }

    public _28806WiltingFlowersFallingTears() : base(28806)
    {
    }

    public override void Register()
    {
        foreach (int butlerId in butlers)
        {
            qe.RegisterQuestNpc(butlerId).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(butlerId).AddOnTalkEvent(questId);
        }
        qe.RegisterQuestNpc(830530).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(830211).AddOnTalkEvent(questId);
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
                case 830530:
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
                case 830211:
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
            if (targetId == 830211)
            {
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }
}
