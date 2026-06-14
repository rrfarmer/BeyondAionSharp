using Aion.GameServer.Model;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _21036DeliveryofAetherSample : AbstractQuestHandler
{
    public _21036DeliveryofAetherSample() : base(21036)
    {
    }

    public override void Register()
    {
        int[] npcs = { 799258, 799238, 798713, 799239 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(799258).AddOnQuestStart(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        if (SendQuestNoneDialog(env, 799258, 182207832, 1))
            return true;

        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;

        int var = qs.GetQuestVarById(0);
        if (qs.GetStatus() == QuestStatus.START)
        {
            if (env.GetTargetId() == 799238)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 0)
                            return SendQuestDialog(env, 1352);
                        return false;
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1);
                }
            }
            else if (env.GetTargetId() == 798713)
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        if (var == 1)
                            return SendQuestDialog(env, 1693);
                        return false;
                    case DialogAction.SETPRO2:
                        return DefaultCloseDialog(env, 1, 2, true, false);
                }
            }
        }
        return SendQuestRewardDialog(env, 799239, 2375);
    }
}
