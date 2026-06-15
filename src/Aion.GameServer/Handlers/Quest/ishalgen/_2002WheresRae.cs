using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Mr. Poke, Hellboy, Gigi, Bobobear, Majka
/// </summary>
public class _2002WheresRae : AbstractQuestHandler
{
	public _2002WheresRae() : base(2002)
	{
	}

	public override void Register()
	{
		int[] npc_ids = { 203519, 203534, 203553, 700045, 203516, 203538 };
		qe.RegisterOnQuestCompleted(questId);
		qe.RegisterOnLevelChanged(questId);
		qe.RegisterQuestNpc(210377).AddOnKillEvent(questId);
		qe.RegisterQuestNpc(210378).AddOnKillEvent(questId);
		foreach (int npc_id in npc_ids)
			qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		QuestEnv qEnv = env;
		if (qs == null)
			return false;

		int var = qs.GetQuestVarById(0);
		int targetId = env.GetTargetId();
		int dialogActionId = env.GetDialogActionId();
		if (qs.GetStatus() == QuestStatus.START)
		{
			switch (targetId)
			{
				case 203519:
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
								PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
								return true;
							}
							break;
					}
					return false;
				case 203534:
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 1)
								return SendQuestDialog(env, 1352);
							return false;
						case DialogAction.SELECT2_1:
							PlayQuestMovie(env, 52);
							break;
						case DialogAction.SETPRO2:
							if (var == 1)
							{
								qs.SetQuestVarById(0, var + 1);
								UpdateQuestStatus(env);
								PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
								return true;
							}
							break;
					}
					return false;
				case 790002:
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 2)
								return SendQuestDialog(env, 1693);
							else if (var == 10)
								return SendQuestDialog(env, 2034);
							else if (var == 11)
								return SendQuestDialog(env, 2375);
							else if (var == 12)
								return SendQuestDialog(env, 2462);
							else if (var == 13)
								return SendQuestDialog(env, 2716);
							return false;
						case DialogAction.SETPRO3:
						case DialogAction.SETPRO4:
						case DialogAction.SETPRO6:
							if (var == 2 || var == 10)
							{
								qs.SetQuestVarById(0, var + 1);
								UpdateQuestStatus(env);
								PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
								return true;
							}
							else if (var == 13)
							{
								qs.SetQuestVarById(0, 14);
								UpdateQuestStatus(env);
								PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
								return true;
							}
							break;
						case DialogAction.SETPRO5:
							if (var == 12 || var == 99)
							{
								qs.SetQuestVar(99);
								UpdateQuestStatus(env);
								PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 0));
								// Create instance
								WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.ATAXIAR.GetId(), player);
								TeleportService.TeleportTo(player, newInstance, 457.65f, 426.8f, 230.4f);
								return true;
							}
							return false;
						case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
							if (var == 11)
							{
								if (QuestService.CollectItemCheck(env, true))
								{
									qs.SetQuestVarById(0, 12);
									UpdateQuestStatus(env);
									return SendQuestDialog(env, 2461);
								}
								else
									return SendQuestDialog(env, 2376);
							}
							break;
					}
					break;
				case 700045:
					if (var == 11 && env.GetDialogActionId() == DialogAction.USE_OBJECT)
					{
						Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(8343, player, player);
						return true;
					}
					return false;
				case 203538:
					if (var == 14 && env.GetDialogActionId() == DialogAction.USE_OBJECT)
					{
						qs.SetQuestVarById(0, var + 1);
						UpdateQuestStatus(env);
						Npc npc = (Npc)env.GetVisibleObject();
						PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(npc.GetObjectId(), 10));
						SpawnForFiveMinutes(203553, npc.GetPosition());
						npc.GetController().DeleteAndScheduleRespawn();
						return true;
					}
					break;
				case 203553:
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 15)
								return SendQuestDialog(env, 3057);
							return false;
						case DialogAction.SETPRO7:
							if (var == 15)
							{
								env.GetVisibleObject().GetController().Delete();
								qs.SetStatus(QuestStatus.REWARD);
								UpdateQuestStatus(env);
								PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
								return true;
							}
							break;
					}
					return false;
				case 205020:
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var >= 12)
							{
								player.SetState(CreatureState.FLYING);
								player.UnsetState(CreatureState.ACTIVE);
								player.SetFlightTeleportId(3001);
								PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 3001, 0));
								ThreadPoolManager.GetInstance().Schedule(ct =>
								{
									TeleportService.TeleportTo(player, 220010000, 940.15f, 2295.64f, 265.7f, (byte)43);
									qs.SetQuestVar(13);
									UpdateQuestStatus(qEnv);
									return ValueTask.CompletedTask;
								}, 40000L);
								return true;
							}
							return false;
						default:
							return false;
					}
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 203516)
			{
				if (dialogActionId == DialogAction.USE_OBJECT || dialogActionId == DialogAction.QUEST_SELECT)
					return SendQuestDialog(env, 3398);
				else if (dialogActionId == DialogAction.SETPRO8)
					return SendQuestDialog(env, 5);
				else
					return SendQuestEndDialog(env);
			}
		}
		return false;
	}

	public override bool OnKillEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs == null)
			return false;

		int var = qs.GetQuestVarById(0);
		int targetId = 0;
		if (env.GetVisibleObject() is Npc npc)
			targetId = npc.GetNpcId();

		if (qs.GetStatus() != QuestStatus.START)
			return false;
		switch (targetId)
		{
			case 210377:
			case 210378:
				if (var >= 3 && var < 10)
				{
					qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
					UpdateQuestStatus(env);
					return true;
				}
				break;
		}
		return false;
	}

	public override void OnQuestCompletedEvent(QuestEnv env)
	{
		DefaultOnQuestCompletedEvent(env, 2100);
	}

	public override void OnLevelChangedEvent(Player player)
	{
		DefaultOnLevelChangedEvent(player, 2100);
	}
}
