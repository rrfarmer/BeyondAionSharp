using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

public class _2798SignontheDottedLine : AbstractQuestHandler
{
    public _2798SignontheDottedLine() : base(2798)
    {
    }

    public override void Register()
    {
        int[] npcs = { 279007, 263569, 263267, 264769, 271054, 266554, 270152, 269252, 268052, 260236 };
        foreach (int npc in npcs)
            qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
        qe.RegisterQuestNpc(279007).AddOnQuestStart(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        if (SendQuestNoneDialog(env, 279007, 4762, 182205646, 1))
            return true;
        QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
        if (qs == null)
            return false;
        int var = qs.GetQuestVarById(0);
        if (env.GetTargetId() == 263569)
        {
            if (qs.GetStatus() == QuestStatus.START && var == 0)
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1011);
                    case DialogAction.SETPRO1:
                        return DefaultCloseDialog(env, 0, 1);
                }
        }
        else if (env.GetTargetId() == 263267)
        {
            if (qs.GetStatus() == QuestStatus.START && var == 1)
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1352);
                    case DialogAction.SETPRO2:
                        return DefaultCloseDialog(env, 1, 2);
                }
        }
        else if (env.GetTargetId() == 264769)
        {
            if (qs.GetStatus() == QuestStatus.START && var == 2)
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 1693);
                    case DialogAction.SETPRO3:
                        return DefaultCloseDialog(env, 2, 3);
                }
        }
        else if (env.GetTargetId() == 271054)
        {
            if (qs.GetStatus() == QuestStatus.START && var == 3)
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2034);
                    case DialogAction.SETPRO4:
                        return DefaultCloseDialog(env, 3, 4);
                }
        }
        else if (env.GetTargetId() == 266554)
        {
            if (qs.GetStatus() == QuestStatus.START && var == 4)
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2375);
                    case DialogAction.SETPRO5:
                        return DefaultCloseDialog(env, 4, 5);
                }
        }
        else if (env.GetTargetId() == 270152)
        {
            if (qs.GetStatus() == QuestStatus.START && var == 5)
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 2716);
                    case DialogAction.SETPRO6:
                        return DefaultCloseDialog(env, 5, 6);
                }
        }
        else if (env.GetTargetId() == 269252)
        {
            if (qs.GetStatus() == QuestStatus.START && var == 6)
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 3057);
                    case DialogAction.SETPRO7:
                        return DefaultCloseDialog(env, 6, 7);
                }
        }
        else if (env.GetTargetId() == 268052)
        {
            if (qs.GetStatus() == QuestStatus.START && var == 7)
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 3398);
                    case DialogAction.SETPRO8:
                        return DefaultCloseDialog(env, 7, 8);
                }
        }
        else if (env.GetTargetId() == 260236)
        {
            if (qs.GetStatus() == QuestStatus.START && var == 8)
                switch (env.GetDialogActionId())
                {
                    case DialogAction.QUEST_SELECT:
                        return SendQuestDialog(env, 3739);
                    case DialogAction.SET_SUCCEED:
                        return DefaultCloseDialog(env, 8, 8, true, false);
                }
        }
        return SendQuestRewardDialog(env, 279007, 10002);
    }
}
