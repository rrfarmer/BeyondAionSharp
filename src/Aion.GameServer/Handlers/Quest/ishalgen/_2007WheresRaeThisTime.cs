using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Teleport;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr. Poke, Majka
/// </summary>
public class _2007WheresRaeThisTime : AbstractQuestHandler
{
	public _2007WheresRaeThisTime() : base(2007)
	{
	}

	public override void Register()
	{
		int[] talkNpcs = { 203516, 203519, 203539, 203552, 203554, 700085, 700086, 700087 };
		qe.RegisterOnQuestCompleted(questId);
		qe.RegisterOnLevelChanged(questId);
		foreach (int id in talkNpcs)
			qe.RegisterQuestNpc(id).AddOnTalkEvent(questId);
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs == null)
			return false;

		int var = qs.GetQuestVarById(0);
		int targetId = env.GetTargetId();
		int dialogActionId = env.GetDialogActionId();

		if (qs.GetStatus() == QuestStatus.START)
		{
			switch (targetId)
			{
				case 203516:
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 0)
								return SendQuestDialog(env, 1011);
							return false;
						case DialogAction.SETPRO1:
							if (var == 0)
							{
								qs.SetQuestVarById(0, var + 1);
								UpdateQuestStatus(env);
								CloseDialogWindow(env);
								return true;
							}
							break;
					}
					break;
				case 203519:
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 1)
								return SendQuestDialog(env, 1352);
							return false;
						case DialogAction.SETPRO2:
							if (var == 1)
							{
								qs.SetQuestVarById(0, var + 1);
								UpdateQuestStatus(env);
								CloseDialogWindow(env);
								return true;
							}
							break;
					}
					break;
				case 203539:
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 2)
								return SendQuestDialog(env, 1693);
							return false;
						case DialogAction.SELECT3_1:
							PlayQuestMovie(env, 55);
							break;
						case DialogAction.SETPRO3:
							if (var == 2)
							{
								qs.SetQuestVarById(0, var + 1);
								UpdateQuestStatus(env);
								CloseDialogWindow(env);
								return true;
							}
							break;
					}
					break;
				case 203552:
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 3)
								return SendQuestDialog(env, 2034);
							return false;
						case DialogAction.SETPRO4:
							if (var == 3)
							{
								qs.SetQuestVarById(0, var + 1);
								UpdateQuestStatus(env);
								CloseDialogWindow(env);
								return true;
							}
							break;
					}
					break;
				case 203554:
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 4)
								return SendQuestDialog(env, 2375);
							else if (var == 8)
								return SendQuestDialog(env, 2716);
							return false;
						case DialogAction.SETPRO5:
							if (var == 4)
							{
								qs.SetQuestVar(5);
								UpdateQuestStatus(env);
								CloseDialogWindow(env);
								return true;
							}
							break;
						case DialogAction.SETPRO6:
							if (var == 8)
							{
								qs.SetQuestVar(9);
								UpdateQuestStatus(env);
								qs.SetQuestVar(8);
								qs.SetStatus(QuestStatus.REWARD);
								UpdateQuestStatus(env);
								CloseDialogWindow(env);
								TeleportService.TeleportToNpc(player, 203516);
								return true;
							}
							break;
					}
					break;
				case 700085:
					if (var == 5)
					{
						Destroy(6, env);
						return false;
					}
					break;
				case 700086:
					if (var == 6)
					{
						Destroy(7, env);
						return false;
					}
					break;
				case 700087:
					if (var == 7)
					{
						Destroy(-1, env);
						return false;
					}
					break;
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 203516)
			{
				if (dialogActionId == DialogAction.USE_OBJECT)
				{
					PlayQuestMovie(env, 58);
					return SendQuestDialog(env, 3057);
				}
				else
					return SendQuestEndDialog(env);
			}
		}
		return false;
	}

	public override void OnQuestCompletedEvent(QuestEnv env)
	{
		int[] quests = { 2006, 2005, 2004, 2003, 2002, 2001 };
		DefaultOnQuestCompletedEvent(env, quests);
	}

	public override void OnLevelChangedEvent(Player player)
	{
		int[] quests = { 2100, 2006, 2005, 2004, 2003, 2002, 2001 };
		DefaultOnLevelChangedEvent(player, quests);
	}

	private void Destroy(int var, QuestEnv env)
	{
		Player player = env.GetPlayer();
		// sendEmotion(env, player, EmotionId.STAND, true); //wrong emotion and source of it - rechk on retail
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		switch (var)
		{
			case 6:
			case 7:
				qs.SetQuestVar(var);
				break;
			case -1:
				PlayQuestMovie(env, 56);
				qs.SetQuestVar(8);
				break;
		}
		UpdateQuestStatus(env);
	}
}
