using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Enomine
    /// </summary>
    public class _24031EnemyAtTheDoorstep : AbstractQuestHandler
    {
        public _24031EnemyAtTheDoorstep() : base(24031)
        {
        }

        public override void Register()
        {
            int[] npc_ids = { 204052, 801224, 203550, 203654, 204369 };
            qe.RegisterOnQuestCompleted(questId);
            qe.RegisterOnLevelChanged(questId);
            qe.RegisterOnEnterWorld(questId);
            qe.RegisterQuestItem(182215394, questId);
            qe.RegisterQuestItem(182215395, questId);
            qe.RegisterQuestItem(182215396, questId);
            qe.RegisterQuestNpc(233879).AddOnKillEvent(questId);
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            int targetId = env.GetTargetId();
            int dialogActionId = env.GetDialogActionId();
            if (qs == null)
                return false;
            if (qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                switch (targetId)
                {
                    case 204052:// vidar
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1011);
                            case DialogAction.SETPRO1:
                                qs.SetQuestVar(1);
                                UpdateQuestStatus(env);
                                return CloseDialogWindow(env);
                        }
                        break;
                    case 801224:// Rapidfire Rita
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1352);
                            case DialogAction.SETPRO2:
                                qs.SetQuestVar(2);
                                UpdateQuestStatus(env);
                                return CloseDialogWindow(env);
                        }
                        break;
                    case 203550:// Munin
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 1693);
                            case DialogAction.SETPRO3:
                                if (!GiveQuestItem(env, 182215394, 1))
                                    return true;
                                qs.SetQuestVar(3);
                                UpdateQuestStatus(env);
                                return CloseDialogWindow(env);
                        }
                        break;
                    case 203654:// Aurtri
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                return SendQuestDialog(env, 2375);
                            case DialogAction.SETPRO5:
                                if (!GiveQuestItem(env, 182215395, 1))
                                    return true;
                                qs.SetQuestVar(5);
                                UpdateQuestStatus(env);
                                return CloseDialogWindow(env);
                        }
                        break;
                    case 204369:// Tyr
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 6)
                                {
                                    return SendQuestDialog(env, 3057);
                                }
                                if (var == 8)
                                {
                                    return SendQuestDialog(env, 3739);
                                }
                                return false;
                            case DialogAction.SETPRO7:
                                if (!GiveQuestItem(env, 182215396, 1))
                                    return true;
                                qs.SetQuestVar(7);
                                UpdateQuestStatus(env);
                                return CloseDialogWindow(env);
                            case DialogAction.SETPRO9:
                                qs.SetQuestVar(9);
                                UpdateQuestStatus(env);
                                return CloseDialogWindow(env);
                        }
                        break;
                    case 730888: // Teleporter Device
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                // TODO:play movie find movie ID
                                env.GetVisibleObject().GetController().Delete();
                                qs.SetQuestVar(11);
                                UpdateQuestStatus(env);
                                Spawn(730898, player, (float) 262.9, (float) 224.5, (float) 212.2, (byte) 95); // Broken Teleporter Device
                                break;
                        }
                        break;
                    case 730898: // Broken Teleporter
                        switch (dialogActionId)
                        {
                            case DialogAction.QUEST_SELECT:
                                env.GetVisibleObject().GetController().Delete();
                                qs.SetStatus(QuestStatus.REWARD);
                                UpdateQuestStatus(env);
                                break;
                        }
                        break;
                }
            }
            if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204052)// vidar
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            return SendQuestDialog(env, 4083);
                        case DialogAction.SELECT_QUEST_REWARD:
                            return SendQuestDialog(env, 5);
                        default:
                            {
                                return SendQuestEndDialog(env);
                            }
                    }
            }
            return false;
        }

        public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (player.IsInsideItemUseZone(ZoneName.Get("DF1_ITEMUSEAREA_Q24031")))
                {
                    if (var == 3)
                    {
                        return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 3, 4, false));// 3-4
                    }
                }
                if (player.IsInsideItemUseZone(ZoneName.Get("DF1A_ITEMUSEAREA_Q24031")))
                {
                    if (var == 5)
                    {
                        return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 5, 6, false));// 5-6
                    }
                }
                if (player.IsInsideItemUseZone(ZoneName.Get("DF2_ITEMUSEAREA_Q24031")))
                {
                    if (var == 7)
                    {
                        return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 7, 8, false));// 7-8
                    }
                }
            }
            return HandlerResult.SUCCESS;
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVars().GetQuestVars();
                if (var == 9 && player.GetWorldId() == 320040000)
                {
                    SpawnForFiveMinutes(730888, player.GetWorldMapInstance(), (float) 262.9, (float) 224.5, (float) 211.2, (byte) 95);// Shattered Large Teleporter
                    SpawnForFiveMinutes(233879, player.GetWorldMapInstance(), (float) 262.9, (float) 224.5, (float) 211.2, (byte) 95);// Captain Hagarkan
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
                if (env.GetTargetId() == 233879)
                    env.GetVisibleObject().GetController().Delete();
                return DefaultOnKillEvent(env, 233879, 9, 10);
            }
            return false;
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player, 24030);
        }

        public override void OnQuestCompletedEvent(QuestEnv env)
        {
            DefaultOnQuestCompletedEvent(env, 24030);
        }
    }
}
