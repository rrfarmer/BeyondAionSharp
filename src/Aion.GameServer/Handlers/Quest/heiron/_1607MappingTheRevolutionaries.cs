using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author vlog
    /// </summary>
    public class _1607MappingTheRevolutionaries : AbstractQuestHandler
    {
        public _1607MappingTheRevolutionaries() : base(1607)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(204578).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204574).AddOnTalkEvent(questId);
            qe.RegisterOnEnterZone(ZoneName.Get("MUDTHORN_EXPERIMENT_LAB_210040000"), questId);
            qe.RegisterOnEnterZone(ZoneName.Get("ROTRON_EXPERIMENT_LAB_210040000"), questId);
            qe.RegisterOnEnterZone(ZoneName.Get("PRETOR_EXPERIMENT_LAB_210040000"), questId);
            qe.RegisterOnEnterZone(ZoneName.Get("POISON_EXTRACTION_LAB_210040000"), questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            int dialogActionId = env.GetDialogActionId();
            int targetId = env.GetTargetId();
            if (qs == null || qs.IsStartable())
            {
                switch (env.GetDialogActionId())
                {
                    case DialogAction.ASK_QUEST_ACCEPT:
                        return SendQuestDialog(env, 4);
                    case DialogAction.QUEST_ACCEPT_1:
                        QuestService.StartQuest(env);
                        return CloseDialogWindow(env);
                    case DialogAction.QUEST_REFUSE_1:
                        return CloseDialogWindow(env);
                }
                return false;
            }

            if (qs.GetStatus() == QuestStatus.START)
            {
                if (targetId == 204578)
                { // Kuobe
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            return SendQuestDialog(env, 1011);
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, 0, 1); // 1
                    }
                }
                else if (targetId == 204574)
                { // Finn
                    if (dialogActionId == DialogAction.QUEST_SELECT)
                    {
                        int var = qs.GetQuestVarById(0);
                        int var1 = qs.GetQuestVarById(1);
                        int var2 = qs.GetQuestVarById(2);
                        int var3 = qs.GetQuestVarById(3);
                        int var4 = qs.GetQuestVarById(4);
                        if (var == 1 && var1 == 1 && var2 == 1 && var3 == 1 && var4 == 1)
                        {
                            return SendQuestDialog(env, 10002);
                        }
                    }
                    else if (dialogActionId == DialogAction.SELECT_QUEST_REWARD)
                    {
                        ChangeQuestStep(env, 1, 1, true);
                        return SendQuestDialog(env, 5);
                    }
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204574)
                { // Finn
                    return SendQuestEndDialog(env);
                }
            }
            return false;
        }

        public override bool OnEnterZoneEvent(QuestEnv env, ZoneName zoneName)
        {
            Player player = env.GetPlayer();
            if (player == null)
                return false;
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (var == 1)
                {
                    if (zoneName == ZoneName.Get("MUDTHORN_EXPERIMENT_LAB_210040000"))
                        return ChangeQuestStep(env, 0, 1, false, 1); // 1: 1
                    if (zoneName == ZoneName.Get("ROTRON_EXPERIMENT_LAB_210040000"))
                        return ChangeQuestStep(env, 0, 1, false, 2); // 2: 1
                    if (zoneName == ZoneName.Get("PRETOR_EXPERIMENT_LAB_210040000"))
                        return ChangeQuestStep(env, 0, 1, false, 3); // 3: 1
                    if (zoneName == ZoneName.Get("POISON_EXTRACTION_LAB_210040000"))
                        return ChangeQuestStep(env, 0, 1, false, 4); // 4: 1
                }
            }
            return false;
        }
    }
}
