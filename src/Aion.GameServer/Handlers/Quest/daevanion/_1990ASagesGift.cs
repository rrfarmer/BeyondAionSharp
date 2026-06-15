using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author kecimis, Tiger, vlog, Neon
/// </summary>
public class _1990ASagesGift : AbstractQuestHandler
{
	public _1990ASagesGift() : base(1990)
	{
	}

	public override void Register()
	{
		int[] mobs = { 256617, 253721, 253720, 254514, 254513 };
		qe.RegisterQuestNpc(203771).AddOnQuestStart(questId);
		qe.RegisterQuestNpc(203771).AddOnTalkEvent(questId);
		foreach (int mob in mobs)
		{
			qe.RegisterQuestNpc(mob).AddOnKillEvent(questId);
		}
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		int dialogActionId = env.GetDialogActionId();
		int targetId = env.GetTargetId();

		if (qs == null || qs.IsStartable())
		{
			if (targetId == 203771) // Fermina
			{
				if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
				{
					if (IsDaevanionArmorEquipped(player))
					{
						return SendQuestDialog(env, 4762);
					}
					else
					{
						return SendQuestDialog(env, 4848);
					}
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
			int var1 = qs.GetQuestVarById(1);
			if (targetId == 203771) // Fermina
			{
				switch (dialogActionId)
				{
					case DialogAction.QUEST_SELECT:
						if (var == 0)
						{
							return SendQuestDialog(env, 1011);
						}
						else if (var == 1)
						{
							return SendQuestDialog(env, 10000);
						}
						else if (var == 2 && var1 == 60)
						{
							return SendQuestDialog(env, 1693);
						}
						else if (var == 3)
						{
							return SendQuestDialog(env, 2034);
						}
						return false;
					case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
						return CheckQuestItems(env, 0, 1, false, 10000, 10001); // 1
					case DialogAction.SELECT4_1:
						int currentDp = player.GetCommonData().GetDp();
						int maxDp = player.GetGameStats().GetMaxDp().GetCurrent();
						long burner = player.GetInventory().GetItemCountByItemId(186000040); // Divine Incense Burner
						if (currentDp == maxDp && burner >= 1)
						{
							RemoveQuestItem(env, 186000040, 1);
							player.GetCommonData().SetDp(0);
							ChangeQuestStep(env, 3, 3, true); // reward
							return SendQuestDialog(env, 5);
						}
						else
						{
							return SendQuestDialog(env, 2120);
						}
					case DialogAction.SETPRO2:
						return DefaultCloseDialog(env, 1, 2); // 2
					case DialogAction.SETPRO3:
						qs.SetQuestVar(3); // 3
						UpdateQuestStatus(env);
						return SendQuestSelectionDialog(env);
					case DialogAction.FINISH_DIALOG:
						return SendQuestSelectionDialog(env);
				}
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 203771) // Fermina
			{
				return SendQuestEndDialog(env);
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
			int all;
			int a = (qs.GetQuestVars().GetQuestVars() >> 7) & 0x7F;
			int b = (qs.GetQuestVars().GetQuestVars() >> 14) & 0x7F;
			int c = (qs.GetQuestVars().GetQuestVars() >> 21) & 0x7F;
			int var = qs.GetQuestVarById(0);
			if (var == 2)
			{
				switch (env.GetTargetId())
				{
					case 256617: // Strange Lake Spirit
						if (a >= 0 && a < 30)
						{
							++a;
							all = c;
							all = all << 7;
							all += b;
							all = all << 7;
							all += a;
							all = all << 7;
							all += 2; // var0
							qs.SetQuestVar(all);
							UpdateQuestStatus(env);
						}
						break;
					case 253721:
					case 253720: // Lava Hoverstone
						if (b >= 0 && b < 30)
						{
							++b;
							all = c;
							all = all << 7;
							all += b;
							all = all << 7;
							all += a;
							all = all << 7;
							all += 2; // var0
							qs.SetQuestVar(all);
							UpdateQuestStatus(env);
						}
						break;
					case 254514:
					case 254513: // Disturbed Resident
						if (c >= 0 && c < 30)
						{
							++c;
							all = c;
							all = all << 7;
							all += b;
							all = all << 7;
							all += a;
							all = all << 7;
							all += 2; // var0
							qs.SetQuestVar(all);
							UpdateQuestStatus(env);
						}
						break;
				}
				if (qs.GetQuestVarById(0) == 2 && a == 30 && b == 30 && c == 30)
				{
					qs.SetQuestVarById(1, 60);
					UpdateQuestStatus(env);
					return true;
				}
			}
		}
		return false;
	}

	private bool IsDaevanionArmorEquipped(Player player)
	{
		int plate = player.GetEquipment().ItemSetPartsEquipped(9);
		int chain = player.GetEquipment().ItemSetPartsEquipped(8);
		int leather = player.GetEquipment().ItemSetPartsEquipped(7);
		int cloth = player.GetEquipment().ItemSetPartsEquipped(6);
		int gunner = player.GetEquipment().ItemSetPartsEquipped(378);
		if (plate == 5 || chain == 5 || leather == 5 || cloth == 5 || gunner == 5)
		{
			return true;
		}
		return false;
	}
}
