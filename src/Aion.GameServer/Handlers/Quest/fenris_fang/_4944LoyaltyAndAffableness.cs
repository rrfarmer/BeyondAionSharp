using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Quest starter: Kvasir (204053). Collect the amulets (600) from Brohum Warriors and Brohum Hunters and take them to Kvasir. Defeat Great Protectors
/// in the Eye of Reshanta (300): Aether's Defender (251002), Fire's Defender (251021), Ancient Defender (251018), Nature's Defender (251039), Light's
/// Defender (251033), Shadow's Defender (251036). Talk with Kvasir. Go to the Dredgion and kill a Dredgion Captains (1): Captain Adhati (214823),
/// Captain Mituna (216850). Talk with Kvasir. Fill yourself with Divine Power and take Mysterious Holy Water (186000086) to High Priest Balder
/// (204075) for the final blessing ritual. Talk with Kvasir.
///
/// @author vlog
/// </summary>
public class _4944LoyaltyAndAffableness : AbstractQuestHandler
{
	private static readonly int[] npcs = { 204053, 204075 };
	private static readonly int[] mobs = { 251002, 251021, 251018, 251039, 251033, 251036, 214823, 216850 };

	public _4944LoyaltyAndAffableness() : base(4944)
	{
	}

	public override void Register()
	{
		qe.RegisterQuestNpc(204053).AddOnQuestStart(questId);
		foreach (int npc in npcs)
		{
			qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
		}
		foreach (int mob in mobs)
		{
			qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
		}
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		int targetId = env.GetTargetId();
		int dialogActionId = env.GetDialogActionId();
		if (qs == null)
		{
			if (targetId == 204053) // Kvasir
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
			int var = qs.GetQuestVars().GetQuestVars();
			switch (targetId)
			{
				case 204053: // Kvasir
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 0)
							{
								return SendQuestDialog(env, 1011);
							}
							else if (var == 306)
							{
								return SendQuestDialog(env, 1693);
							}
							else if (var == 4)
							{
								return SendQuestDialog(env, 2375);
							}
							return false;
						case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
							return CheckQuestItems(env, 0, 6, false, 10000, 10001); // 6
						case DialogAction.FINISH_DIALOG:
							return DefaultCloseDialog(env, 0, 0);
						case DialogAction.SETPRO3:
							qs.SetQuestVar(3); // 3
							UpdateQuestStatus(env);
							return SendQuestSelectionDialog(env);
						case DialogAction.SETPRO5:
							return DefaultCloseDialog(env, 4, 5); // 5
					}
					break;
				case 204075: // Balder
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 5)
							{
								return SendQuestDialog(env, 2716);
							}
							return false;
						case DialogAction.SELECT6_1_1:
							if (player.GetCommonData().GetDp() >= 4000)
							{
								return CheckItemExistence(env, 5, 5, false, 186000087, 1, true, 2718, 2887, 0, 0);
							}
							else
							{
								return SendQuestDialog(env, 2802);
							}
						case DialogAction.SET_SUCCEED:
							player.GetCommonData().SetDp(0);
							return DefaultCloseDialog(env, 5, 5, true, false); // reward
						case DialogAction.FINISH_DIALOG:
							return DefaultCloseDialog(env, 5, 5);
					}
					break;
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 204053) // Kvasir
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

	public override bool OnKillEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		int targetId = env.GetTargetId();
		if (qs != null && qs.GetStatus() == QuestStatus.START)
		{
			int var = qs.GetQuestVars().GetQuestVars();
			if (var >= 6 && var < 306)
			{
				int[] npcids = { 251002, 251021, 251018, 251039, 251033, 251036 };
				foreach (int id in npcids)
				{
					if (targetId == id)
					{
						qs.SetQuestVar(var + 1); // 6 - 306
						UpdateQuestStatus(env);
						return true;
					}
				}
			}
			else if (var == 3)
			{
				int[] npcids = { 214823, 216850 };
				return DefaultOnKillEvent(env, npcids, 3, 4); // 4
			}
		}
		return false;
	}
}
