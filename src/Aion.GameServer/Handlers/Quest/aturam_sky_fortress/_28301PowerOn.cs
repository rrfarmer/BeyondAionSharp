using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author zhkchi, Pad
/// </summary>
public class _28301PowerOn : AbstractQuestHandler
{
    private const int sphereId = 702656;

    public _28301PowerOn() : base(28301)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799530).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799530).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(730374).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(sphereId).AddOnKillEvent(questId);
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
                    PlayQuestMovie(env, 468);
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
            if (targetId == 730374 && qs.GetQuestVarById(0) == 7)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SETPRO2:
                        GiveQuestItem(env, 182212110, 1);
                        qs.SetStatus(QuestStatus.REWARD);
                        UpdateQuestStatus(env);
                        return CloseDialogWindow(env);
                    default:
                        return SendQuestEndDialog(env);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799530)
            {
                switch (dialogActionId)
                {
                    case DialogAction.USE_OBJECT:
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
        int targetId = env.GetTargetId();

        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            int var0 = qs.GetQuestVarById(0);
            if (var0 <= 6 && targetId == sphereId)
            {
                qs.SetQuestVarById(0, var0 + 1);
                UpdateQuestStatus(env);
                return true;
            }
        }
        return false;
    }
}
