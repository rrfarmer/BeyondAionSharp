using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Nephis
/// </summary>
public class _2620SummoningPhagrasul : AbstractQuestHandler
{
	private static readonly int[] mob_ids = { 213109, 213111 };

	public _2620SummoningPhagrasul() : base(2620)
	{
	}

	public override void Register()
	{
		qe.RegisterQuestNpc(204787).AddOnQuestStart(questId); // Chieftain Akagitan
		qe.RegisterQuestNpc(204787).AddOnTalkEvent(questId);
		qe.RegisterQuestNpc(204824).AddOnTalkEvent(questId); // Gigantic Phagrasul
		qe.RegisterQuestNpc(700323).AddOnTalkEvent(questId); // Huge Mamut Skull
		foreach (int mob_id in mob_ids)
			qe.RegisterQuestNpc(mob_id).AddOnKillEvent(questId);
	}

	public override bool OnKillEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs == null || qs.GetStatus() != QuestStatus.START)
			return false;

		int targetId = 0;
		if (env.GetVisibleObject() is Npc npc)
			targetId = npc.GetNpcId();

		switch (targetId)
		{
			case 213109:
				if (qs.GetQuestVarById(1) < 5 && qs.GetQuestVarById(0) == 1)
				{
					qs.SetQuestVarById(1, qs.GetQuestVarById(1) + 1);
					UpdateQuestStatus(env);
					return true;
				}
				break;

			case 213111:
				if (qs.GetQuestVarById(2) < 5 && qs.GetQuestVarById(0) == 1)
				{
					qs.SetQuestVarById(2, qs.GetQuestVarById(2) + 1);
					UpdateQuestStatus(env);
					return true;
				}
				break;
		}

		return false;
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		int targetId = 0;
		if (env.GetVisibleObject() is Npc)
			targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		int dialogActionId = env.GetDialogActionId();

		if (qs == null || qs.IsStartable())
		{
			if (targetId == 204787) // Chieftain Akagitan
			{
				if (dialogActionId == DialogAction.QUEST_SELECT)
					return SendQuestDialog(env, 4762);
				else if (dialogActionId == DialogAction.QUEST_ACCEPT_1)
				{
					if (!GiveQuestItem(env, 182204498, 1))
						return true;
					return SendQuestStartDialog(env);
				}
				else
					return SendQuestStartDialog(env);
			}
		}
		else if (qs.GetStatus() == QuestStatus.START)
		{
			int var = qs.GetQuestVarById(0);
			if (targetId == 204824)
			{
				switch (dialogActionId)
				{
					case DialogAction.QUEST_SELECT:
						if (var == 0)
							return SendQuestDialog(env, 1011);
						return false;
					case DialogAction.SETPRO1:
						if (var == 0)
						{
							qs.SetQuestVarById(0, qs.GetQuestVarById(0) + 1);
							UpdateQuestStatus(env);
							PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
							Npc npc = (Npc)env.GetVisibleObject();
							ThreadPoolManager.GetInstance().Schedule(ct =>
							{
								npc.GetController().Delete();
								return ValueTask.CompletedTask;
							}, 40000L);
							return true;
						}
						break;
				}
			}
			if (targetId == 700323) // Hugh mamut skull
			{
				switch (dialogActionId)
				{
					case DialogAction.USE_OBJECT:
						if (var == 0)
						{
							int targetObjectId = env.GetVisibleObject().GetObjectId();
							PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), targetObjectId, 3000, 1));
							PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.NEUTRALMODE_IN_STANDING, 0, targetObjectId), true);
							ThreadPoolManager.GetInstance().Schedule(ct =>
							{
								RemoveQuestItem(env, 182204498, 1);
								if (player.GetTarget() == null || player.GetTarget().GetObjectId() != targetObjectId)
									return ValueTask.CompletedTask;
								PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), targetObjectId, 3000, 0));
								PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.START_LOOT, 0, targetObjectId), true);
								SpawnForFiveMinutes(204824, player.GetWorldMapInstance(), (float)2851.698, (float)160.88698, (float)301.78537, (byte)93);
								return ValueTask.CompletedTask;
							}, 3000L);
						}
						break;
				}
			}
			if (targetId == 204787) // Chieftain Akagitan
			{
				switch (dialogActionId)
				{
					case DialogAction.USE_OBJECT:
						qs.SetStatus(QuestStatus.REWARD);
						UpdateQuestStatus(env);
						return SendQuestDialog(env, 10002);
					case DialogAction.SELECT_QUEST_REWARD:
						return SendQuestDialog(env, 5);
				}
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 204787)
				return SendQuestEndDialog(env);
		}
		return false;
	}
}
