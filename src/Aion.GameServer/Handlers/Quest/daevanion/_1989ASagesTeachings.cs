using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Rhys2002
/// </summary>
public class _1989ASagesTeachings : AbstractQuestHandler
{
	public _1989ASagesTeachings() : base(1989)
	{
	}

	public override void Register()
	{
		qe.RegisterQuestNpc(203771).AddOnQuestStart(questId);
		qe.RegisterQuestNpc(203771).AddOnTalkEvent(questId);
		qe.RegisterQuestNpc(203704).AddOnTalkEvent(questId);
		qe.RegisterQuestNpc(203705).AddOnTalkEvent(questId);
		qe.RegisterQuestNpc(203706).AddOnTalkEvent(questId);
		qe.RegisterQuestNpc(203707).AddOnTalkEvent(questId);
		qe.RegisterQuestNpc(801214).AddOnTalkEvent(questId);
		qe.RegisterQuestNpc(801215).AddOnTalkEvent(questId);
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();

		int targetId = 0;
		if (env.GetVisibleObject() is Npc npc)
			targetId = npc.GetNpcId();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);

		if (qs == null || qs.IsStartable())
		{
			if (targetId == 203771)
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
				case 203704: // Boreas
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
				case 203705: // Jumentis
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
				case 203706: // Charna
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
				case 203707: // Thrasymedes
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
				case 801214:
					switch (env.GetDialogActionId())
					{
						case DialogAction.QUEST_SELECT:
							if (playerClass == PlayerClass.GUNNER || playerClass == PlayerClass.RIDER)
								return SendQuestDialog(env, 2548);
							else
								return SendQuestDialog(env, 2568);
						case DialogAction.SETPRO1:
							qs.SetQuestVarById(0, var + 1);
							UpdateQuestStatus(env);
							PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
							return true;
					}
					return false;
				case 801215:
					switch (env.GetDialogActionId())
					{
						case DialogAction.QUEST_SELECT:
							if (playerClass == PlayerClass.BARD)
								return SendQuestDialog(env, 2633);
							else
								return SendQuestDialog(env, 2653);
						case DialogAction.SETPRO1:
							qs.SetQuestVarById(0, var + 1);
							UpdateQuestStatus(env);
							PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
							return true;
					}
					return false;
				case 203771:
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
								PlayQuestMovie(env, 105);
								player.GetCommonData().SetDp(0);
								qs.SetStatus(QuestStatus.REWARD);
								UpdateQuestStatus(env);
								return SendQuestDialog(env, 5);
							}
							else if (var == 4)
							{
								PlayQuestMovie(env, 105);
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
			if (targetId == 203771)
				return SendQuestEndDialog(env);
		}
		return false;
	}
}
