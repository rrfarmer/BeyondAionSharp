using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Cheatkiller
/// </summary>
public class _11076ProofOfTalent : AbstractQuestHandler
{
    public _11076ProofOfTalent() : base(11076)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(799025).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(799084).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799025).AddOnTalkEvent(questId);
        qe.RegisterOnEnterWindStream(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();
        int targetId = env.GetTargetId();

        if (qs == null || qs.IsStartable())
        {
            if (targetId == 799025)
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
            if (targetId == 799084)
            {
                if (dialogActionId == DialogAction.QUEST_SELECT)
                {
                    if (qs.GetQuestVarById(0) == 3)
                        return SendQuestDialog(env, 2034);
                }
                else if (dialogActionId == DialogAction.SETPRO4)
                {
                    TeleportService.TeleportTo(player, 210050000, 1338.6f, 279.6f, 590, (byte) 80, TeleportAnimation.FADE_OUT_BEAM);
                    return DefaultCloseDialog(env, 3, 4, true, false);
                }
            }
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            if (targetId == 799025)
            {
                if (dialogActionId == DialogAction.USE_OBJECT)
                {
                    return SendQuestDialog(env, 10002);
                }
                return SendQuestEndDialog(env);
            }
        }
        return false;
    }

    public override bool OnEnterWindStreamEvent(QuestEnv env, int teleportId)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs != null && qs.GetStatus() == QuestStatus.START)
        {
            if (player.GetWorldId() == 210050000)
            {
                if (teleportId == 152001)
                    ChangeQuestStep(env, 0, 1);
                else if (teleportId == 153001)
                    ChangeQuestStep(env, 1, 2);
                else if (teleportId == 154001)
                    ChangeQuestStep(env, 2, 3);
                return true;
            }
        }
        return false;
    }
}
