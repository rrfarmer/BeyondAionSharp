using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @Author Majka
    /// </summary>
    public class _20031GotoGelkmaros : AbstractQuestHandler
    {
        private static readonly int[] mobs = { 216091, 216092, 216093, 216095, 216096, 216097 };

        public _20031GotoGelkmaros() : base(20031)
        {
        }

        public override void Register()
        {
            // Vidar ID: 204052
            // Agehia ID: 798800
            // Tigrina ID: 798409
            // Richelle ID: 799225
            // Merhen ID: 799364
            // Hogidin ID: 799365
            // Valetta ID: 799226
            int[] npcs = { 204052, 798409, 798800, 799225, 799226, 799364, 799365 };
            qe.RegisterOnLevelChanged(questId);
            foreach (int mob in mobs)
            {
                qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
            }
            foreach (int npc in npcs)
            {
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            }
            qe.RegisterQuestItem(182215590, questId);
            qe.RegisterOnEnterWorld(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null)
            {
                return false;
            }
            int var = qs.GetQuestVarById(0);
            int targetId = env.GetTargetId();
            int dialogActionId = env.GetDialogActionId();

            if (qs.GetStatus() == QuestStatus.START)
            {
                if (targetId == 204052)
                { // Vidar
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            break;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, var, var + 1); // 1
                    }
                }
                else if (targetId == 798800)
                { // Agehia
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 1)
                            {
                                return SendQuestDialog(env, 1352);
                            }
                            break;
                        case DialogAction.SETPRO2:
                            return DefaultCloseDialog(env, var, var + 1); // 2
                    }
                }
                else if (targetId == 798409)
                { // Tigrina
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 2)
                            {
                                return SendQuestDialog(env, 1693);
                            }
                            break;
                        case DialogAction.SETPRO3:
                            qs.SetQuestVar(var + 1); // 3
                            UpdateQuestStatus(env);
                            TeleportService.TeleportTo(player, 220070000, player.GetInstanceId(), 1868, 2746, 531, (byte) 20);
                            return CloseDialogWindow(env); // 1
                    }
                }
                else if (targetId == 799225)
                { // Richelle
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 4)
                            {
                                return SendQuestDialog(env, 2375);
                            }
                            break;
                        case DialogAction.SETPRO5:
                            return DefaultCloseDialog(env, var, var + 1); // 5
                    }
                }
                else if (targetId == 799364)
                { // Merhen
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 5)
                            {
                                return SendQuestDialog(env, 2716);
                            }
                            break;
                        case DialogAction.SELECT6_1:
                            PlayQuestMovie(env, 31, true);
                            return SendQuestDialog(env, 2717);
                        case DialogAction.SETPRO6:
                            return DefaultCloseDialog(env, var, var + 1); // 6
                    }
                }
                else if (targetId == 799365)
                { // Hogidin
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 6)
                            {
                                return SendQuestDialog(env, 3057);
                            }
                            break;
                        case DialogAction.SETPRO7:
                            return DefaultCloseDialog(env, var, var + 1); // 7
                    }
                }
                else if (targetId == 799226)
                { // Valetta
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 7)
                            {
                                return SendQuestDialog(env, 3398);
                            }
                            else if (var == 10)
                            {
                                return SendQuestDialog(env, 6500);
                            }
                            break;
                        case DialogAction.SETPRO8:
                            if (var == 7)
                            {
                                GiveQuestItem(env, 182215590, 1);
                                return DefaultCloseDialog(env, var, var + 1); // 8
                            }
                            break;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            if (var == 10)
                            {
                                if (CheckItemExistence(env, 182215591, 1, true))
                                {
                                    qs.SetQuestVar(var + 1); // 11
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    return SendQuestDialog(env, 10000);
                                }
                                else
                                {
                                    return SendQuestDialog(env, 10001);
                                }
                            }
                            break;
                    }
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 799225)
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

        public override bool OnKillEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs != null && qs.GetStatus() == QuestStatus.START)
            {
                int var = qs.GetQuestVarById(0);
                if (var == 8)
                {
                    int var1 = qs.GetQuestVarById(1);
                    if (var1 >= 0 && var1 < 9)
                    {
                        return DefaultOnKillEvent(env, mobs, var1, var1 + 1, 1);
                    }
                    else if (var1 == 9)
                    {
                        qs.SetQuestVar(9); // 9
                        UpdateQuestStatus(env);
                        return true;
                    }
                }
            }
            return false;
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            if (player.GetWorldId() == 220070000)
            {
                if (qs != null && qs.GetStatus() == QuestStatus.START)
                {
                    int var = qs.GetQuestVarById(0);
                    if (var == 3)
                    {
                        PlayQuestMovie(env, 551);
                        return true;
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
                if (var == 9)
                {
                    if (player.IsInsideZone(ZoneName.Get("DF4_ITEMUSEAREA_Q20031_220070000")))
                    {
                        return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 9, 10, false, 566));
                        // playQuestMovie(env, 566);
                        // return HandlerResult.fromBoolean(useQuestItem(env, item, 9, 10, false)); // 10
                    }
                }
            }
            return HandlerResult.FAILED;
        }

        public override void OnMovieEndEvent(QuestEnv env, int movieId)
        {
            if (movieId == 551)
                ChangeQuestStep(env, 3, 4);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player);
        }
    }
}
