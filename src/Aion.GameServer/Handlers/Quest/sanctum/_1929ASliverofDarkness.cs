using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
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
public class _1929ASliverofDarkness : AbstractQuestHandler
{
	public _1929ASliverofDarkness() : base(1929)
	{
	}

	public override void Register()
	{
		int[] npcs = { 203752, 203852, 203164, 205110, 700240, 205111, 203701, 203711 };
		int[] stigmas = { 140000001, 140000002, 140000003, 140000004 };
		qe.RegisterOnLevelChanged(questId);
		qe.RegisterQuestNpc(212992).AddOnKillEvent(questId);
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
		int dialogActionId = env.GetDialogActionId();
		int targetId = env.GetTargetId();
		int var = qs.GetQuestVars().GetQuestVars();

		if (qs.GetStatus() == QuestStatus.START)
		{
			switch (targetId)
			{
				case 203752: // Jucleas
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
				case 203852: // Ludina
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 1)
							{
								return SendQuestDialog(env, 1352);
							}
							return false;
						case DialogAction.SETPRO2:
							if (DefaultCloseDialog(env, 1, 2)) // 2
							{
								TeleportService.TeleportToNpc(player, 203164);
								return true;
							}
							break;
					}
					break;
				case 203164: // Morai
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 2)
							{
								return SendQuestDialog(env, 1693);
							}
							else if (var == 8)
							{
								return SendQuestDialog(env, 3057);
							}
							return false;
						case DialogAction.SETPRO3:
							if (var == 2)
							{
								ChangeQuestStep(env, 2, 93); // 93
								WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.IDLF1B_STIGMA.GetId(), player);
								TeleportService.TeleportTo(player, newInstance, 338, 101, 1191);
								return CloseDialogWindow(env);
							}
							return false;
						case DialogAction.SETPRO7:
							if (DefaultCloseDialog(env, 8, 9)) // 9
							{
								TeleportService.TeleportToNpc(player, 203701);
								return true;
							}
							break;
					}
					break;
				case 205110: // Icaronix
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 93)
							{
								return SendQuestDialog(env, 2034);
							}
							return false;
						case DialogAction.SETPRO4:
							if (var == 93)
							{
								ChangeQuestStep(env, 93, 94); // 94
								player.SetState(CreatureState.FLYING);
								player.UnsetState(CreatureState.ACTIVE);
								player.SetFlightTeleportId(31001);
								PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 31001, 0));
								return true;
							}
							break;
					}
					break;
				case 700240: // Icaronix's Box
				{
					if (dialogActionId == DialogAction.USE_OBJECT)
					{
						if (var == 94)
						{
							return PlayQuestMovie(env, 155);
						}
					}
					break;
				}
				case 205111: // Ecus
					switch (dialogActionId)
					{
						case DialogAction.USE_OBJECT:
							if (var == 96)
							{
								if (IsStigmaEquipped(env))
								{
									return SendQuestDialog(env, 2716);
								}
								else
								{
									PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 1));
									return CloseDialogWindow(env);
								}
							}
							return false;
						case DialogAction.QUEST_SELECT:
							if (var == 98)
							{
								return SendQuestDialog(env, 2375);
							}
							return false;
						case DialogAction.SELECT5_3:
							if (var == 98)
							{
								if (GiveQuestItem(env, GetStoneId(player), 1))
								{
									PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 1));
									return true;
								}
							}
							return false;
						case DialogAction.SELECT6_1_1_1_1:
							if (var == 96)
							{
								Npc npc = (Npc)env.GetVisibleObject();
								npc.GetController().Delete();
								SpawnForFiveMinutes(212992, player.GetWorldMapInstance(), (float)191.9, (float)267.68, 1374, (byte)0);
								ChangeQuestStep(env, 96, 97); // 97
								return CloseDialogWindow(env);
							}
							break;
					}
					break;
				case 203701: // Lavirintos
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 9)
							{
								return SendQuestDialog(env, 3398);
							}
							return false;
						case DialogAction.SETPRO8:
							return DefaultCloseDialog(env, 9, 9, true, false); // reward
					}
					break;
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 203711) // Miriya
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

	public override void OnMovieEndEvent(QuestEnv env, int movieId)
	{
		if (movieId == 155)
		{
			ChangeQuestStep(env, 94, 98);
			SpawnForFiveMinutes(205111, env.GetPlayer().GetWorldMapInstance(), (float)197.6, (float)265.9, (float)1374.0, (byte)0);
		}
	}

	public override bool OnEquipItemEvent(QuestEnv env, int itemId)
	{
		ChangeQuestStep(env, 98, 96); // 96
		return CloseDialogWindow(env);
	}

	public override bool OnKillEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs != null && qs.GetStatus() == QuestStatus.START)
		{
			int var = qs.GetQuestVars().GetQuestVars();
			if (var == 97)
			{
				ChangeQuestStep(env, 97, 8); // 8
				TeleportService.TeleportTo(player, 210030000, 1, 2315.9f, 1800f, 195.2f);
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
			if (var >= 93 && var <= 98)
			{
				RemoveStigma(env);
				ChangeQuestStep(env, var, 2);
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
			if (player.GetWorldId() != 310070000)
			{
				if (var >= 93 && var <= 98)
				{
					RemoveStigma(env);
					ChangeQuestStep(env, var, 2);
					return true;
				}
				else if (var == 8)
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
		switch (player.GetCommonData().GetPlayerClass())
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
				return 0;
		}
	}

	private bool IsStigmaEquipped(QuestEnv env)
	{
		Player player = env.GetPlayer();
		foreach (Item i in player.GetEquipment().GetEquippedItemsAllStigma())
		{
			if (i.GetItemId() == GetStoneId(player))
			{
				return true;
			}
		}
		return false;
	}

	private void RemoveStigma(QuestEnv env)
	{
		Player player = env.GetPlayer();
		foreach (Item item in player.GetEquipment().GetEquippedItemsByItemId(GetStoneId(player)))
		{
			player.GetEquipment().UnEquipItem(item.GetObjectId());
		}
		RemoveQuestItem(env, GetStoneId(player), 1);
	}

	public override void OnLevelChangedEvent(Player player)
	{
		DefaultOnLevelChangedEvent(player);
	}
}
