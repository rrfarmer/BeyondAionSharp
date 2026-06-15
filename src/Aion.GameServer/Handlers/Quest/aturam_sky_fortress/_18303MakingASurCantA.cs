using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi, Pad
/// </summary>
public class _18303MakingASurCantA : AbstractQuestHandler
{
    public _18303MakingASurCantA() : base(18303)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799530).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799530).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730390).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(700980).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(804820).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(217382).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(217376).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799530)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    return SendQuestDialog(env, 4762);
                }
                else if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
                {
                    PlayQuestMovie(env, 470);
                    return SendQuestStartDialog(env);
                }
                else
                {
                    return SendQuestStartDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (targetId == 730390)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 1007);
                    case DialogAction.SETPRO1:
                        TeleportService.TeleportTo(player, 300240000, 158.88f, 624.42f, 901f, (byte)20);
                        return CloseDialogWindow(env);
                    default:
                        return SendQuestStartDialog(env);
                }
            }
            else if (targetId == 700980)
            {
                return UseQuestObject(env, 2, 3, true, true);
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 804820)
            {
                switch (dialogActionId)
                {
                    case DialogAction.QUEST_SELECT:
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

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (qs.GetQuestVarById(0) == 0)
                return DefaultOnKillEvent(env, 217382, 0, 1);
            else
                return DefaultOnKillEvent(env, 217376, 1, 2);
        }
        return false;
    }
}
