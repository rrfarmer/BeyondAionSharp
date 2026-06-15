using System.Threading.Tasks;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.GeoEngine.Math;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Geo;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Estrayl
/// </summary>
public class _30721OminousDebrisEnergy : AbstractQuestHandler
{
	private const int QUEST_ITEM_ID = 182215698; // Sedative
	private const int START_NPC_ID = 804704; // Eukraton
	private const int TALK_NPC_1_ID = 804870; // Monroe
	private const int TALK_NPC_2_ID = 804868; // Rosalee
	private const int ATTACKER_NPC_ID = 217424; // Mirror Image

	public _30721OminousDebrisEnergy() : base(30721)
	{
	}

	public override void Register()
	{
		qe.RegisterQuestNpc(START_NPC_ID).AddOnQuestStart(questId);
		qe.RegisterQuestNpc(TALK_NPC_1_ID).AddOnTalkEvent(questId);
		qe.RegisterQuestNpc(TALK_NPC_2_ID).AddOnTalkEvent(questId);
		qe.RegisterQuestItem(QUEST_ITEM_ID, questId);
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		QuestState qs = env.GetPlayer().GetQuestStateList().GetQuestState(questId);
		int dialogActionId = env.GetDialogActionId();
		int targetId = env.GetTargetId();

		if (qs == null || qs.IsStartable())
		{
			if (targetId == START_NPC_ID)
			{
				if (dialogActionId == DialogAction.QUEST_SELECT)
				{
					return SendQuestDialog(env, 4762);
				}
				else
				{
					return SendQuestStartDialog(env);
				}
			}
		}
		else if (qs.GetStatus() == QuestStatus.START)
		{
			int var = qs.GetQuestVarById(0);
			if (targetId == TALK_NPC_1_ID && var == 0)
			{
				if (dialogActionId == DialogAction.QUEST_SELECT)
				{
					return SendQuestDialog(env, 1011);
				}
				else if (dialogActionId == DialogAction.SETPRO1)
				{
					ChangeQuestStep(env, 0, 1);
					return CloseDialogWindow(env);
				}
			}
			else if (targetId == TALK_NPC_2_ID)
			{
				if (dialogActionId == DialogAction.QUEST_SELECT)
				{
					if (var == 1)
					{
						return SendQuestDialog(env, 1352);
					}
					else if (var == 3)
					{
						return SendQuestDialog(env, 2034);
					}
				}
				else if (dialogActionId == DialogAction.SETPRO2)
				{
					ChangeQuestStep(env, 1, 2);
					GiveQuestItem(env, QUEST_ITEM_ID, 1);
					return CloseDialogWindow(env);
				}
				else if (dialogActionId == DialogAction.SET_SUCCEED)
				{
					ChangeQuestStep(env, 3, 4, true);
					return CloseDialogWindow(env);
				}
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == TALK_NPC_1_ID)
			{
				if (dialogActionId == DialogAction.QUEST_SELECT)
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

	public override HandlerResult OnItemUseEvent(QuestEnv env, Item item)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs != null && qs.GetStatus() == QuestStatus.START)
		{
			if (player.IsInsideItemUseZone(ZoneName.Get("LF5_ITEMUSEAREA_Q30721")))
			{
				int var = qs.GetQuestVarById(0);
				if (var == 2)
				{
					bool isItemUseComplete = UseQuestItem(env, item, 2, 3, false);
					if (isItemUseComplete)
					{
						ThreadPoolManager.GetInstance().Schedule(ct =>
						{
							RndSpawnInRange(player, Rnd.NextFloat(4f));
							RndSpawnInRange(player, Rnd.NextFloat(4f));
							return ValueTask.CompletedTask;
						}, 1500L);
					}
					return HandlerResultExtensions.FromBoolean(isItemUseComplete);
				}
			}
		}
		return HandlerResult.FAILED;
	}

	private void RndSpawnInRange(Player player, float distance)
	{
		WorldPosition p = player.GetPosition();
		double angleRadians = System.Math.PI / 180.0 * Rnd.NextFloat(360f);
		float x = p.GetX() + (float)(System.Math.Cos(angleRadians) * distance);
		float y = p.GetY() + (float)(System.Math.Sin(angleRadians) * distance);

		Vector3f pos = GeoService.GetInstance().GetClosestCollision(player, x, y, p.GetZ());
		SpawnTemplate template = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(p.GetMapId(), ATTACKER_NPC_ID, pos.GetX(), pos.GetY(), pos.GetZ(), (byte)0);

		Npc npc = (Npc)Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(template, p.GetInstanceId());
		npc.GetAggroList().AddHate(player, 1000);
	}
}
