using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _2948HuronsLetter : AbstractQuestHandler
{
    public _2948HuronsLetter() : base(2948)
    {
    }

    public override void Register()
    {
        qe.RegisterOnLevelChanged(questId);
        qe.RegisterQuestNpc(204274).AddOnTalkEvent(questId);
    }

    public override void OnLevelChangedEvent(Player player)
    {
        DefaultOnLevelChangedEvent(player);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int targetId = 0;
        if (env.GetVisibleObject() is Npc npc)
            targetId = npc.GetNpcId();
        if (targetId != 204274)
            return false;
        if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                return SendQuestDialog(env, 10002);
            else if (env.GetDialogActionId() == DialogAction.SELECT_QUEST_REWARD)
            {
                qs.SetStatus(QuestStatus.REWARD);
                qs.SetQuestVarById(0, 1);
                UpdateQuestStatus(env);
                return SendQuestDialog(env, 5);
            }
            return false;
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }
}
