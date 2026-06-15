using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Artur, Majka
/// </summary>
public class _24045ASpeedyErrand : AbstractQuestHandler
{
	private static readonly int[] npc_ids = { 278034, 279004, 279024, 279006 };

	public _24045ASpeedyErrand() : base(24045)
	{
	}

	public override void Register()
	{
		qe.RegisterOnQuestCompleted(questId);
		qe.RegisterOnLevelChanged(questId);
		foreach (int npc_id in npc_ids)
			qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
	}

	public override void OnQuestCompletedEvent(QuestEnv env)
	{
		DefaultOnQuestCompletedEvent(env, 24040);
	}

	public override void OnLevelChangedEvent(Player player)
	{
		DefaultOnLevelChangedEvent(player, 24040);
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs == null)
			return false;

		int var = qs.GetQuestVarById(0);
		int targetId = 0;
		if (env.GetVisibleObject() is Npc npc)
			targetId = npc.GetNpcId();

		if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 278034)
			{
				if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
					return SendQuestDialog(env, 10002);
				return SendQuestEndDialog(env);
			}
			return false;
		}
		else if (qs.GetStatus() != QuestStatus.START)
		{
			return false;
		}
		if (targetId == 278034)
		{
			switch (env.GetDialogActionId())
			{
				case DialogAction.QUEST_SELECT:
					if (var == 0)
						return SendQuestDialog(env, 1011);
					return false;
				case DialogAction.SETPRO1:
					return DefaultCloseDialog(env, 0, 1);
			}
		}
		else if (targetId == 279004)
		{
			switch (env.GetDialogActionId())
			{
				case DialogAction.QUEST_SELECT:
					if (var == 1)
						return SendQuestDialog(env, 1352);
					return false;
				case DialogAction.SELECT2_1:
					PlayQuestMovie(env, 292);
					break;
				case DialogAction.SETPRO2:
					return DefaultCloseDialog(env, 1, 2);
			}
		}
		else if (targetId == 279024)
		{
			switch (env.GetDialogActionId())
			{
				case DialogAction.QUEST_SELECT:
					if (var == 2)
						return SendQuestDialog(env, 1693);
					else if (var == 4)
						return SendQuestDialog(env, 2375);
					return false;
				case DialogAction.SETPRO3:
					if (DefaultCloseDialog(env, 2, 3))
					{
						player.SetState(CreatureState.FLYING);
						player.UnsetState(CreatureState.ACTIVE);
						player.SetFlightTeleportId(55001);
						PacketSendUtility.BroadcastPacketAndReceive(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 55001, 0));
						return true;
					}
					return false;
				case DialogAction.SETPRO5:
					return DefaultCloseDialog(env, 4, 4, true, false);
			}
		}
		else if (targetId == 279006)
		{
			switch (env.GetDialogActionId())
			{
				case DialogAction.QUEST_SELECT:
					if (var == 3)
						return SendQuestDialog(env, 2034);
					return false;
				case DialogAction.SETPRO4:
					if (DefaultCloseDialog(env, 3, 4))
					{
						player.SetState(CreatureState.FLYING);
						player.UnsetState(CreatureState.ACTIVE);
						player.SetFlightTeleportId(56001);
						PacketSendUtility.BroadcastPacketAndReceive(player, new SM_EMOTION(player, EmotionType.START_FLYTELEPORT, 56001, 0));
						return true;
					}
					return false;
			}
		}
		return false;
	}
}
