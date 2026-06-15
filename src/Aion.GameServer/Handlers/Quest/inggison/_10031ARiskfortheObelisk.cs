using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
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
    public class _10031ARiskfortheObelisk : AbstractQuestHandler
    {
        private static readonly int[] mobs = { 215504, 215505, 215516, 215517, 215518, 215519, 216463, 216464, 216647, 216648, 216691, 216692, 216782, 216783,
            215508, 215509 };

        public _10031ARiskfortheObelisk() : base(10031)
        {
        }

        public override void Register()
        {
            // Fasimedes ID: 203700
            // Southern Obelisk Support ID: 702662
            // Overheated Obelisk ID: 730224
            // Sibylle ID: 798408
            // Eremitia ID: 798600
            // Outremus ID: 798926
            // Versetti ID: 798927
            // Steropes ID: 799052
            int[] npcs = { 203700, 702662, 730224, 798408, 798600, 798926, 798927, 799052 };
            qe.RegisterOnLevelChanged(questId);
            foreach (int mob in mobs)
            {
                qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
            }
            foreach (int npc in npcs)
            {
                qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
            }
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
                if (targetId == 203700)
                { // Fasimedes
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                            {
                                return SendQuestDialog(env, 1011);
                            }
                            return false;
                        case DialogAction.SETPRO1:
                            return DefaultCloseDialog(env, var, var + 1); // 1
                    }
                }
                else if (targetId == 798600)
                { // Eremitia
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
                else if (targetId == 798408)
                { // Sibylle
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
                            TeleportService.TeleportTo(player, 210050000, player.GetInstanceId(), 1440, 408, 553, (byte) 77, TeleportAnimation.FADE_OUT_BEAM);
                            return CloseDialogWindow(env); // 1
                    }
                }
                else if (targetId == 798926)
                { // Outremus
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
                else if (targetId == 799052)
                { // Steropes
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 5)
                            {
                                return SendQuestDialog(env, 2716);
                            }
                            break;
                        case DialogAction.SELECT6_1:
                            PlayQuestMovie(env, 30, true);
                            return SendQuestDialog(env, 2717);
                        case DialogAction.SETPRO6:
                            return DefaultCloseDialog(env, var, var + 1); // 6
                    }
                }
                else if (targetId == 798927)
                { // Versetti
                    switch (dialogActionId)
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 6)
                            {
                                return SendQuestDialog(env, 3057);
                            }
                            break;
                        case DialogAction.SELECT7_1:
                            PlayQuestMovie(env, 516);
                            return SendQuestDialog(env, 3058);
                        case DialogAction.SETPRO7:
                            GiveQuestItem(env, 182215616, 1); // Aether Wave Stone
                            GiveQuestItem(env, 182215617, 1); // Obelisk
                            return DefaultCloseDialog(env, var, var + 1); // 7
                    }
                }
                else if (targetId == 730224)
                { // Overheated Obelisk
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            if (var == 7)
                            {
                                RemoveQuestItem(env, 182215616, 1); // Aether Wave Stone
                                return DefaultCloseDialog(env, var, var + 1); // 8
                            }
                            break;
                    }
                }
                else if (targetId == 702662)
                { // Southern Obelisk Support
                    switch (dialogActionId)
                    {
                        case DialogAction.USE_OBJECT:
                            if (var == 8)
                            {
                                RemoveQuestItem(env, 182215617, 1); // Obelisk
                                SpawnTemporarily(700600, player.GetWorldMapInstance(), 2192, 368, 431, (byte) 90, 1); // Enhanced Obelisk
                                qs.SetQuestVar(9); // 9
                                UpdateQuestStatus(env);
                                return true;
                            }
                            break;
                    }
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 798927)
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
                if (var == 9)
                {
                    int[] basrasas = { 215504, 215505, 215516, 215517, 215518, 215519, 216463, 216464, 216647, 216648, 216691, 216692, 216782, 216783 };
                    int[] spallers = { 215508, 215509 };

                    if (DefaultOnKillEvent(env, basrasas, 0, 10, 1) || DefaultOnKillEvent(env, spallers, 0, 2, 2))
                    {
                        int var1 = qs.GetQuestVarById(1);
                        int var2 = qs.GetQuestVarById(2);
                        if (var1 == 10 && var2 == 2)
                        {
                            qs.SetStatus(QuestStatus.REWARD);
                            qs.SetQuestVar(10);
                            UpdateQuestStatus(env);
                        }
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
                        PlayQuestMovie(env, 566);
                        return HandlerResultExtensions.FromBoolean(UseQuestItem(env, item, 9, 10, false)); // 10
                    }
                }
            }
            return HandlerResult.FAILED;
        }

        public override bool OnEnterWorldEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            if (player.GetWorldId() == 210050000)
            {
                if (qs != null && qs.GetStatus() == QuestStatus.START)
                {
                    int var = qs.GetQuestVarById(0);
                    if (var == 3)
                    {
                        PlayQuestMovie(env, 501);
                        return true;
                    }
                }
            }
            return false;
        }

        public override void OnMovieEndEvent(QuestEnv env, int movieId)
        {
            if (movieId == 501)
                ChangeQuestStep(env, 3, 4);
        }

        public override void OnLevelChangedEvent(Player player)
        {
            DefaultOnLevelChangedEvent(player);
        }
    }
}
