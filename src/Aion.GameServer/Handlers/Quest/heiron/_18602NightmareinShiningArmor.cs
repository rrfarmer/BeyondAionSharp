using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Gigi, Majka
/// </summary>
public class _18602NightmareinShiningArmor : AbstractQuestHandler
{
	// Raninia 205229
	// Maga's Potions 730308
	// Robstin's Corpse 700939
	private static readonly int[] npc_ids = { 205229, 730308, 700939 };

	public _18602NightmareinShiningArmor() : base(18602)
	{
	}

	public override void Register()
	{
		qe.RegisterOnEnterWorld(questId);
		qe.RegisterOnDie(questId);
		foreach (int npc_id in npc_ids)
			qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
		qe.RegisterQuestNpc(205229).AddOnQuestStart(questId);
		qe.RegisterQuestNpc(217005).AddOnKillEvent(questId); // Shadow Judge Kaliga
	}

	public override bool OnEnterWorldEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs != null && qs.GetStatus() == QuestStatus.START)
		{
			if (player.GetWorldId() != 300230000)
			{
				int var = qs.GetQuestVarById(0);
				if (var > 0)
				{
					ChangeQuestStep(env, var, 0);
					return true;
				}
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
			int var = qs.GetQuestVarById(0);
			if (var > 0)
			{
				ChangeQuestStep(env, var, 0);
				return true;
			}
		}
		return false;
	}

	public override bool OnKillEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs != null && qs.GetStatus() == QuestStatus.START)
		{
			return DefaultOnKillEvent(env, 217005, 3, true); // Shadow Judge Kaliga
		}
		return false;
	}

	public override void OnMovieEndEvent(QuestEnv env, int movieId)
	{
		if (movieId == 454)
			ChangeQuestStep(env, 1, 2);
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		int targetId = env.GetTargetId();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);

		if (qs == null || qs.IsStartable())
		{
			if (targetId == 205229)
			{
				if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
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
			if (targetId == 205229)
			{
				if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
				{
					return SendQuestDialog(env, 1011);
				}
				else if (env.GetDialogActionId() == DialogAction.SETPRO1)
				{
					WorldMapInstance newInstance = InstanceService.GetNextAvailableInstance(WorldMapType.KROMEDES_TRIAL.GetId(), player);
					TeleportService.TeleportTo(player, newInstance, 244.98566f, 244.14162f, 189.52058f, (byte)30, TeleportAnimation.FADE_OUT_BEAM);
					ChangeQuestStep(env, 0, 1); // 1
					return CloseDialogWindow(env);
				}
			}
			else if (targetId == 730308)
			{
				if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
				{
					if (var == 1)
					{
						return SendQuestDialog(env, 1352);
					}
				}
				else if (env.GetDialogActionId() == DialogAction.SELECT2_1)
				{
					return SendQuestDialog(env, 1353);
				}
				else if (env.GetDialogActionId() == DialogAction.SETPRO2)
				{
					// Check item
					if (var == 1)
					{
						if (CheckItemExistence(env, 185000109, 1, true)) // Hisen's key
						{
							ChangeQuestStep(env, 1, 2); // 2
							TeleportService.TeleportTo(player, 300230000, 687.56116f, 681.68225f, 200.28648f, (byte)30, TeleportAnimation.FADE_OUT_BEAM);
							return CloseDialogWindow(env);
						}
						else
						{
							return SendQuestDialog(env, 10001);
						}
					}
					else
					{
						TeleportService.TeleportTo(player, 300230000, 687.56116f, 681.68225f, 200.28648f, (byte)30, TeleportAnimation.FADE_OUT_BEAM);
						return CloseDialogWindow(env);
					}
				}
			}
			else if (targetId == 700939)
			{
				if (env.GetDialogActionId() == DialogAction.USE_OBJECT)
				{
					if (var == 2)
					{
						return SendQuestDialog(env, 1693);
					}
				}
				else if (env.GetDialogActionId() == DialogAction.SETPRO3)
				{
					if (!player.GetEffectController().HasAbnormalEffect(19288))
					{
						Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(19288, player, player); // Rage of Kromede
					}
					return DefaultCloseDialog(env, 2, 3);
				}
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 205229)
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
}
