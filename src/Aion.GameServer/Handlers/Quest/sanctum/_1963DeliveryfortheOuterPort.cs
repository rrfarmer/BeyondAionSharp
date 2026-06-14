using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _1963DeliveryfortheOuterPort : AbstractQuestHandler
{
    public _1963DeliveryfortheOuterPort() : base(1963)
    {
    }

    public override void Register()
    {
        int[] npcs = { 203726, 203851 };
        qe.RegisterQuestNpc(203726).AddOnQuestStart(questId);
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        if (SendQuestNoneDialog(env, 203726, 182206032, 1))
            return true;

        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);

        if (qs == null)
            return false;
        int var = qs.GetQuestVarById(0);

        if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 203851)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO1:
                        ChangeQuestStep(env, 0, 1);
                        return DefaultCloseDialog(env, 1, 1, true, false, 0, 0, 182206032, 1);
                }
            }
        }
        return SendQuestRewardDialog(env, 203726, 2375);
    }
}
