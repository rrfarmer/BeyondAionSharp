using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Majka
/// </summary>
public class _24043LazyLanguageLessons : AbstractQuestHandler
{
	public _24043LazyLanguageLessons() : base(24043)
	{
	}

	public override void Register()
	{
		int[] npcs = { 278003, 278086, 278039, 279027, 204210 };
		qe.RegisterOnQuestCompleted(questId);
		qe.RegisterOnLevelChanged(questId);
		qe.RegisterQuestNpc(253610).AddOnAttackEvent(questId);
		qe.RegisterQuestNpc(253611).AddOnAttackEvent(questId);
		foreach (int npc in npcs)
		{
			qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
		}
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		int dialogActionId = env.GetDialogActionId();
		if (qs == null)
			return false;
		int var = qs.GetQuestVarById(0);
		int targetId = env.GetTargetId();

		if (qs.GetStatus() == QuestStatus.START)
		{
			switch (targetId)
			{
				case 278003: // Hisui
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 0)
							{
								return SendQuestDialog(env, 1011);
							}
							return false;
						case DialogAction.SETPRO1:
							return DefaultCloseDialog(env, 0, 1); // 1
					}
					break;
				case 278086: // Sinjah
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 1)
							{
								return SendQuestDialog(env, 1352);
							}
							return false;
						case DialogAction.SETPRO2:
							return DefaultCloseDialog(env, 1, 2); // 2
					}
					break;
				case 278039: // Grunn
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 3)
							{
								return SendQuestDialog(env, 2034);
							}
							return false;
						case DialogAction.SETPRO4:
							return DefaultCloseDialog(env, 3, 4); // 4
					}
					break;
				case 279027: // Kaoranerk
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 4)
							{
								return SendQuestDialog(env, 2375);
							}
							else if (var == 6)
							{
								return SendQuestDialog(env, 3057);
							}
							return false;
						case DialogAction.SELECT7_1:
							RemoveQuestItem(env, 182215373, 1);
							PlayQuestMovie(env, 293);
							return SendQuestDialog(env, 3058);
						case DialogAction.SETPRO5:
							return DefaultCloseDialog(env, 4, 5); // 5
						case DialogAction.SET_SUCCEED:
							return DefaultCloseDialog(env, 6, 6, true, false); // reward
					}
					break;
				case 204210: // Phosphor
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 5)
							{
								return SendQuestDialog(env, 2716);
							}
							return false;
						case DialogAction.SETPRO6:
							return DefaultCloseDialog(env, 5, 6, 182215373, 1, 0, 0); // 6
					}
					break;
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 278003) // Hisui
			{
				if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
				{
					return SendQuestDialog(env, 10002);
				}
				else
				{
					return SendQuestEndDialog(env);
				}
			}
		}
		return false;
	}

	public override bool OnAttackEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs != null && qs.GetStatus() == QuestStatus.START)
		{
			int var = qs.GetQuestVarById(0);
			if (var == 2)
			{
				Npc npc = (Npc)env.GetVisibleObject();
				SpawnSearchResult searchResult = DataManager.SPAWNS_DATA.GetFirstSpawnByNpcId(npc.GetWorldId(), 278086); // Sinjah
				if (PositionUtil.IsInRange(npc, searchResult.GetSpot().GetX(), searchResult.GetSpot().GetY(), searchResult.GetSpot().GetZ(), 15))
				{
					npc.GetController().Die(player);
					ChangeQuestStep(env, 2, 3); // 3
					return true;
				}
			}
		}
		return true;
	}

	public override void OnQuestCompletedEvent(QuestEnv env)
	{
		DefaultOnQuestCompletedEvent(env, 24040);
	}

	public override void OnLevelChangedEvent(Player player)
	{
		DefaultOnLevelChangedEvent(player, 24040);
	}
}
