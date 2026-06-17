using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr. Poke, Rolandas, vlog
/// </summary>
public class _2900NoEscapingDestiny : AbstractQuestHandler
{
	public _2900NoEscapingDestiny() : base(2900)
	{
	}

	public override void Register()
	{
		int[] npcs = { 204182, 203550, 790003, 790002, 203546, 204264, 204061 };
		int[] stigmas = { 140000001, 140000002, 140000003, 140000004 };
		qe.RegisterOnLevelChanged(questId);
		qe.RegisterQuestNpc(204263).AddOnKillEvent(questId);
		qe.RegisterOnEnterWorld(questId);
		qe.RegisterOnDie(questId);
		foreach (int npc in npcs)
		{
			qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
		}
		foreach (int stigma in stigmas)
		{
			qe.RegisterOnEquipItem(stigma, questId);
		}
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs == null)
		{
			return false;
		}
		int var = qs.GetQuestVars().GetQuestVars();
		int targetId = env.GetTargetId();
		int dialogActionId = env.GetDialogActionId();

		if (qs.GetStatus() == QuestStatus.START)
		{
			switch (targetId)
			{
				case 204182: // Heimdall
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 0)
							{
								return SendQuestDialog(env, 1011);
							}
							return false;
						case DialogAction.SETPRO1:
							if (DefaultCloseDialog(env, 0, 1)) // 1
							{
								TeleportService.TeleportTo(player, 220010000, 1, 389.0f, 1896.0f, 327.5f, (byte)61, TeleportAnimation.FADE_OUT_BEAM);
								return true;
							}
							break;
					}
					break;
				case 203550: // Munin
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 1)
							{
								return SendQuestDialog(env, 1352);
							}
							else if (var == 10)
							{
								return SendQuestDialog(env, 4080);
							}
							return false;
						case DialogAction.SETPRO2:
							return DefaultCloseDialog(env, 1, 2); // 2
						case DialogAction.SETPRO10:
							DefaultCloseDialog(env, 10, 10, true, false); // reward
							TeleportService.TeleportTo(player, 120010000, 1294.8f, 1213.8f, 214.34f, (byte)30, TeleportAnimation.FADE_OUT_BEAM);
							return true;
					}
					break;
				case 790003: // Urd
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 2)
							{
								return SendQuestDialog(env, 1693);
							}
							return false;
						case DialogAction.SETPRO3:
							return DefaultCloseDialog(env, 2, 3); // 3
					}
					break;
				case 790002: // Verdandi
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
				case 203546: // Skuld
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 4)
							{
								return SendQuestDialog(env, 2375);
							}
							else if (var == 9)
							{
								return SendQuestDialog(env, 3739);
							}
							return false;
						case DialogAction.SETPRO5:
							if (var == 4)
							{
								ChangeQuestStep(env, 4, 95); // 95
								WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.SPACE_OF_DESTINY.GetId(), player);
								TeleportService.TeleportTo(player, newInstance, 270.8424f, 249.1182f, 125.8369f, (byte)60, TeleportAnimation.FADE_OUT_BEAM);
								return CloseDialogWindow(env);
							}
							return false;
						case DialogAction.SETPRO9:
							ChangeQuestStep(env, 9, 10); // 10
							TeleportService.TeleportTo(player, 220010000, 1, 383.0f, 1896.0f, 327.625f, (byte)60, TeleportAnimation.FADE_OUT_BEAM);
							return CloseDialogWindow(env);
					}
					break;
				case 204264: // Skuld 2
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 95)
							{
								return SendQuestDialog(env, 2716);
							}
							else if (var == 96 || var == 99)
							{
								return SendQuestDialog(env, 3057);
							}
							else if (var == 97)
							{
								return SendQuestDialog(env, 3398);
							}
							return false;
						case DialogAction.SETPRO6:
							if (var == 95)
							{
								PlayQuestMovie(env, 156);
								return CloseDialogWindow(env);
							}
							return false;
						case DialogAction.SELECT7_1:
							if (GiveQuestItem(env, GetStoneId(player), 1))
								ChangeQuestStep(env, 96, 99); // 99
							return SendQuestDialog(env, 3058);
						case DialogAction.SETPRO7:
							if (var == 99)
							{
								PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), DialogPage.STIGMA.Id()));
								return true;
							}
							return false;
						case DialogAction.SETPRO8:
							if (var == 97)
							{
								ChangeQuestStep(env, 97, 98); // 98
								SpawnForFiveMinutes(204263, player.GetWorldMapInstance(), 257.5f, 245f, 125f, (byte)0);
								return CloseDialogWindow(env);
							}
							break;
					}
					break;
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 204061) // Aud
			{
				return SendQuestEndDialog(env);
			}
		}
		return false;
	}

	public override void OnMovieEndEvent(QuestEnv env, int movieId)
	{
		if (movieId == 156)
			ChangeQuestStep(env, 95, 96);
	}

	public override bool OnEquipItemEvent(QuestEnv env, int itemId)
	{
		ChangeQuestStep(env, 99, 97); // 97
		return CloseDialogWindow(env);
	}

	public override bool OnKillEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs != null && qs.GetStatus() == QuestStatus.START)
		{
			int var = qs.GetQuestVars().GetQuestVars();
			if (var == 98)
			{
				ChangeQuestStep(env, 98, 9); // 9
				TeleportService.TeleportTo(player, 220010000, 1, 1112.492f, 1718.974f, 270.45917f, (byte)113, TeleportAnimation.FADE_OUT_BEAM);
				return true;
			}
		}
		return false;
	}

	public override bool OnDieEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs != null && qs.GetStatus() == QuestStatus.START)
		{
			int var = qs.GetQuestVars().GetQuestVars();
			if (var >= 95 && var <= 99)
			{
				RemoveStigma(env);
				ChangeQuestStep(env, var, 4);
				return true;
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
			if (player.GetWorldId() != 320070000)
			{
				if (var >= 95 && var <= 99)
				{
					RemoveStigma(env);
					ChangeQuestStep(env, var, 4);
					return true;
				}
				else if (var == 9)
				{
					RemoveStigma(env);
					return true;
				}
			}
		}
		return false;
	}

	private int GetStoneId(Player player)
	{
		// TODO: find out the correct stigma ids for each class on official servers
		switch (player.GetPlayerClass())
		{
			case PlayerClass.CHANTER:
			case PlayerClass.CLERIC:
			case PlayerClass.BARD:
				return 140000001; // Healight Light II
			case PlayerClass.RIDER:
			case PlayerClass.GUNNER:
			case PlayerClass.RANGER:
				return 140000002; // Flame Cage I
			case PlayerClass.GLADIATOR:
			case PlayerClass.ASSASSIN:
			case PlayerClass.TEMPLAR:
				return 140000003; // Ferocious Strike III (melee weapon required)
			case PlayerClass.SORCERER:
			case PlayerClass.SPIRIT_MASTER:
				return 140000004; // Hydro Eruption II
			default:
				// Java parity: data/handlers/quest/pandaemonium/_2900NoEscapingDestiny.java throws UnsupportedOperationException
					throw new System.NotSupportedException("Unhandled player class " + player.GetPlayerClass());
		}
	}

	private void RemoveStigma(QuestEnv env)
	{
		Player player = env.GetPlayer();
		int stigmaId = GetStoneId(player);
		foreach (Item item in player.GetEquipment().GetEquippedItemsByItemId(stigmaId))
		{
			player.GetEquipment().UnEquipItem(item.GetObjectId());
		}
		RemoveQuestItem(env, stigmaId, 1);
	}

	public override void OnLevelChangedEvent(Player player)
	{
		DefaultOnLevelChangedEvent(player);
	}
}
