using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Hilgert
/// </summary>
public class _2843OperationAnnihilate : AbstractQuestHandler
{
    public _2843OperationAnnihilate() : base(2843)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(268081).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(268081).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(215134).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215136).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215122).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215124).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215116).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215315).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215299).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215112).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215120).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215125).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215118).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215119).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215312).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215109).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215097).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215304).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215308).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215292).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215101).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215126).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215098).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215305).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215289).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215300).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215113).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215316).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215094).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215301).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215313).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215110).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215297).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215314).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215111).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215298).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215104).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215096).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215303).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215287).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215128).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215121).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215106).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215293).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215107).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215100).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215291).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215307).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215290).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215099).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215306).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215123).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215108).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215295).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215311).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215095).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215302).AddOnKillEvent(questId);
        qe.RegisterQuestNpc(215133).AddOnKillEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();

        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null || qs.IsStartable())
        {
            if (env.GetTargetId() == 268081)
            {
                if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    return SendQuestDialog(env, 1011);
                else
                    return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 268081)
                return true;
        }
        else if (qs.GetStatus() == QuestStatus.REWARD && env.GetTargetId() == 268081)
        {
            qs.SetQuestVarById(0, 0);
            UpdateQuestStatus(env);
            return SendQuestEndDialog(env);
        }
        return false;
    }

    public override bool OnKillEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null || qs.GetStatus() != QuestStatus.START)
            return false;

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (player.GetPosition().GetMapId() == 300140000)
            {
                if (qs.GetQuestVarById(0) < 79)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    UpdateQuestStatus(env);
                    return true;
                }
                else if (qs.GetQuestVarById(0) == 79 || qs.GetQuestVarById(0) > 79)
                {
                    qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
                    qs.SetStatus(QuestStatus.REWARD);
                    UpdateQuestStatus(env);
                    return true;
                }
            }
        }
        return false;
    }
}
