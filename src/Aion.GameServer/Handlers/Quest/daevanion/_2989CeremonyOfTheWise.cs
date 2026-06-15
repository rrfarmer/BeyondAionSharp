using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author Rhys2002
    /// </summary>
    public class _2989CeremonyOfTheWise : AbstractQuestHandler
    {
        public _2989CeremonyOfTheWise() : base(2989)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(204146).AddOnQuestStart(questId);
            qe.RegisterQuestNpc(204146).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204056).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204057).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204058).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(204059).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(801222).AddOnTalkEvent(questId);
            qe.RegisterQuestNpc(801223).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();

            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            if (qs == null || qs.IsStartable())
            {
                if (targetId == 204146)
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                        return SendQuestDialog(env, 1011);
                    else
                        return SendQuestStartDialog(env);
                }
            }

            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);

            if (qs.GetStatus() == QuestStatus.START)
            {
                PlayerClass playerClass = player.GetCommonData().GetPlayerClass();
                switch (targetId)
                {
                    case 204056: // Traufnir
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (playerClass == PlayerClass.GLADIATOR || playerClass == PlayerClass.TEMPLAR)
                                    return SendQuestDialog(env, 1352);
                                else
                                    return SendQuestDialog(env, 1438);
                            case DialogAction.SETPRO1:
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                        }
                        return false;
                    case 204057: // Sigyn
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (playerClass == PlayerClass.ASSASSIN || playerClass == PlayerClass.RANGER)
                                    return SendQuestDialog(env, 1693);
                                else
                                    return SendQuestDialog(env, 1779);
                            case DialogAction.SETPRO1:
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                        }
                        return false;
                    case 204058: // Sif
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (playerClass == PlayerClass.SORCERER || playerClass == PlayerClass.SPIRIT_MASTER)
                                    return SendQuestDialog(env, 2034);
                                else
                                    return SendQuestDialog(env, 2120);
                            case DialogAction.SETPRO1:
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                        }
                        return false;
                    case 204059: // Freyr
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (playerClass == PlayerClass.CLERIC || playerClass == PlayerClass.CHANTER)
                                    return SendQuestDialog(env, 2375);
                                else
                                    return SendQuestDialog(env, 2461);
                            case DialogAction.SETPRO1:
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                        }
                        return false;
                    case 801222:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (playerClass == PlayerClass.GUNNER || playerClass == PlayerClass.RIDER)
                                    return SendQuestDialog(env, 2546);
                                else
                                    return SendQuestDialog(env, 2589);
                            case DialogAction.SETPRO1:
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                        }
                        return false;
                    case 801223:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (playerClass == PlayerClass.BARD)
                                    return SendQuestDialog(env, 2631);
                                else
                                    return SendQuestDialog(env, 2674);
                            case DialogAction.SETPRO1:
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                        }
                        return false;
                    case 204146:
                        switch (env.GetDialogActionId())
                        {
                            case DialogAction.QUEST_SELECT:
                                if (var == 1)
                                    return SendQuestDialog(env, 2716);
                                else if (var == 2)
                                    return SendQuestDialog(env, 3057);
                                else if (var == 3)
                                {
                                    if (player.GetCommonData().GetDp() < 4000)
                                        return SendQuestDialog(env, 3484);
                                    else
                                        return SendQuestDialog(env, 3398);
                                }
                                else if (var == 4)
                                {
                                    if (player.GetCommonData().GetDp() < 4000)
                                        return SendQuestDialog(env, 3825);
                                    else
                                        return SendQuestDialog(env, 3739);
                                }
                                return false;
                            case DialogAction.SELECT_QUEST_REWARD:
                                if (var == 3)
                                {
                                    PlayQuestMovie(env, 137);
                                    player.GetCommonData().SetDp(0);
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    return SendQuestDialog(env, 5);
                                }
                                else if (var == 4)
                                {
                                    PlayQuestMovie(env, 137);
                                    player.GetCommonData().SetDp(0);
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    return SendQuestDialog(env, 5);
                                }
                                else
                                    return this.SendQuestEndDialog(env);
                            case DialogAction.SETPRO2:
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                return SendQuestDialog(env, 3057);
                            case DialogAction.SETPRO4:
                                qs.SetQuestVarById(0, 3);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                            case DialogAction.SETPRO5:
                                qs.SetQuestVarById(0, 4);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                        }
                        break;
                }
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204146)
                    return SendQuestEndDialog(env);
            }
            return false;
        }
    }
}
