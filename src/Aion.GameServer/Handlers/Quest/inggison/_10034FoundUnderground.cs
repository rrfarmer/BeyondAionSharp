using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Majka
/// </summary>
public class _10034FoundUnderground : AbstractQuestHandler
{
	public _10034FoundUnderground() : base(10034)
	{
	}

	public override void Register()
	{
		// Sueros ID: 799030
		// Honeus ID: 799029
		// Titus ID: 798990
		// Drakan Stone Statue ID: 730295
		// Hidden Switch ID: 700604
		// Traveler's bag ID: 730229
		int[] npcs = { 799030, 799029, 798990, 730295, 700604, 730229 };
		qe.AddHandlerSideQuestDrop(questId, 730229, 182215628, 1, 100);
		foreach (int npc in npcs)
		{
			qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
		}
		qe.RegisterOnEnterWorld(questId);
		qe.RegisterQuestItem(182215628, questId);
		qe.RegisterQuestNpc(216531).AddOnKillEvent(questId);
		qe.RegisterOnQuestCompleted(questId);
		qe.RegisterOnLevelChanged(questId);
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs == null)
		{
			return false;
		}
		int dialogActionId = env.GetDialogActionId();
		int var = qs.GetQuestVarById(0);
		int targetId = env.GetTargetId();

		if (qs.GetStatus() == QuestStatus.START)
		{
			switch (targetId)
			{
				case 799030: // Sueros
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 0)
							{
								return SendQuestDialog(env, 1011);
							}
							return false;
						case DialogAction.SETPRO1:
							return DefaultCloseDialog(env, 0, 1);
					}
					break;
				case 799029:
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 1)
							{
								return SendQuestDialog(env, 1352);
							}
							return false;
						case DialogAction.SETPRO2:
							return DefaultCloseDialog(env, 1, 2);
					}
					break;
				case 798990:
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 2)
							{
								return SendQuestDialog(env, 1693);
							}
							return false;
						case DialogAction.SELECT3_1_1:
							PlayQuestMovie(env, 504);
							return SendQuestDialog(env, 1695);
						case DialogAction.SETPRO3:
							return DefaultCloseDialog(env, 2, 3);
					}
					break;
				case 730295:
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 3)
							{
								return SendQuestDialog(env, 2034);
							}
							break;
						case DialogAction.SETPRO4:
							if (var == 3)
							{
								if (RemoveQuestItem(env, 182215627, 1))
								{
									WorldMapInstance instance = InstanceService.GetNextAvailableInstance(WorldMapType.UDAS_TEMPLE_LOWER.GetId(), player);
									TeleportService.TeleportTo(player, instance, 795.28143f, 918.806f, 149.80243f, (byte)73, TeleportAnimation.FADE_OUT_BEAM);
									return true;
								}
								else
								{
									return SendQuestDialog(env, 10001);
								}
							}
							break;
					}
					break;
				case 700604:
					if (var == 4 && dialogActionId == DialogAction.USE_OBJECT)
					{
						IInstanceHandler instanceHandler = player.GetWorldMapInstance().GetInstanceHandler();
						instanceHandler.HandleUseItemFinish(player, (Npc)env.GetVisibleObject());
						return UseQuestObject(env, 4, 5, false, 0);
					}
					break;
				case 730229:
					if (var == 6)
					{
						if (dialogActionId == DialogAction.QUEST_SELECT)
						{
							return SendQuestDialog(env, 3057);
						}

						if (dialogActionId == DialogAction.SETPRO7)
						{
							GiveQuestItem(env, 182215628, 1);
							return DefaultCloseDialog(env, 6, 7);
						}
					}
					break;
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 799030)
			{
				if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
				{
					return SendQuestDialog(env, 10002);
				}
				return SendQuestEndDialog(env);
			}
		}
		return false;
	}

	public override bool OnEnterWorldEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs != null && qs.GetStatus() == QuestStatus.START)
		{
			int var = qs.GetQuestVars().GetQuestVars();
			if (var == 3)
			{
				if (player.GetWorldId() == 300160000)
				{
					qs.SetQuestVar(4);
					UpdateQuestStatus(env);
					return true;
				}
			}
			else if (var >= 4 && var < 7)
			{
				if (player.GetWorldId() != 300160000)
				{
					qs.SetQuestVar(3);
					UpdateQuestStatus(env);
					return true;
				}
			}
		}
		return false;
	}

	public override bool OnKillEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs == null || qs.GetStatus() != QuestStatus.START)
		{
			return false;
		}
		int var = qs.GetQuestVarById(0);
		switch (env.GetTargetId())
		{
			case 216531:
				if (var == 5)
				{
					SpawnForFiveMinutes(730229, player.GetWorldMapInstance(), 744, 885, 154, (byte)90); // Traveler's Bag
					SpawnForFiveMinutes(804815, player.GetWorldMapInstance(), 776, 876, 151, (byte)90);
					qs.SetQuestVarById(0, var + 1);
					UpdateQuestStatus(env);
					return true;
				}
				break;
		}
		return false;
	}

	public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
	{
		Player player = env.GetPlayer();
		if (item.GetItemId() == 182215628)
		{
			QuestState qs = player.GetQuestStateList().GetQuestState(questId);
			if (qs != null && qs.GetStatus() == QuestStatus.START)
			{
				qs.SetQuestVar(8);
				qs.SetStatus(QuestStatus.REWARD);
				UpdateQuestStatus(env);
			}
			return HandlerResult.SUCCESS;
		}
		return HandlerResult.FAILED;
	}

	public override void OnQuestCompletedEvent(QuestEnv env)
	{
		DefaultOnQuestCompletedEvent(env, 10031);
	}

	public override void OnLevelChangedEvent(Player player)
	{
		DefaultOnLevelChangedEvent(player, 10031);
	}
}
