using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Nanou, bobobear
/// </summary>
public class _3938WellRounded : AbstractQuestHandler
{
	public _3938WellRounded() : base(3938)
	{
	}

	public override void Register()
	{
		int[] npcs = { 203788, 203792, 203790, 203793, 203784, 203786, 798316, 203752, 203701 };
		qe.RegisterQuestNpc(203701).AddOnQuestStart(questId); // Lavirintos
		foreach (int npc in npcs)
			qe.RegisterQuestNpc(npc).AddOnTalkEvent(questId);
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		int dialogActionId = env.GetDialogActionId();
		int targetId = env.GetTargetId();

		// 0 - Start to Lavirintos
		if (qs == null || qs.IsStartable())
		{
			if (targetId == 203701)
			{
				if (dialogActionId == DialogAction.QUEST_SELECT)
					return SendQuestDialog(env, 4762);
				else
					return SendQuestStartDialog(env);
			}
		}

		if (qs == null)
			return false;

		int var = qs.GetQuestVarById(0);

		if (qs.GetStatus() == QuestStatus.START)
		{
			switch (targetId)
			{
				// 1 - Talk with Lavirintos and choose a crafting skill
				case 203701:
					if (var == 0)
					{
						switch (dialogActionId)
						{
							case DialogAction.QUEST_SELECT:
								return SendQuestDialog(env, 1011);
							case DialogAction.SETPRO1:
								return DefaultCloseDialog(env, 0, 1);
							case DialogAction.SETPRO2:
								return DefaultCloseDialog(env, 0, 2);
							case DialogAction.SETPRO3:
								return DefaultCloseDialog(env, 0, 3);
							case DialogAction.SETPRO4:
								return DefaultCloseDialog(env, 0, 4);
							case DialogAction.SETPRO5:
								return DefaultCloseDialog(env, 0, 5);
							case DialogAction.SETPRO6:
								return DefaultCloseDialog(env, 0, 6);
						}
						break;
					}
					break;
				// 2 - Talk with Weaponsmithing Master Anteros.
				case 203788:
					if (var == 1)
					{
						switch (dialogActionId)
						{
							case DialogAction.QUEST_SELECT:
								return SendQuestDialog(env, 1352);
							case DialogAction.SETPRO7:
								return DefaultCloseDialog(env, 1, 7, 152201596, 1, 0, 0);
						}
					}
					break;
				// 3 - Talk with Handicrafting Master Utsida
				case 203792:
					if (var == 2)
					{
						switch (dialogActionId)
						{
							case DialogAction.QUEST_SELECT:
								return SendQuestDialog(env, 1693);
							case DialogAction.SETPRO7:
								return DefaultCloseDialog(env, 2, 7, 152201639, 1, 0, 0);
						}
					}
					break;
				// 4 - Talk with Armorsmithing Master Vulcanus
				case 203790:
					if (var == 3)
					{
						switch (dialogActionId)
						{
							case DialogAction.QUEST_SELECT:
								return SendQuestDialog(env, 2034);
							case DialogAction.SETPRO7:
								return DefaultCloseDialog(env, 3, 7, 152201615, 1, 0, 0);
						}
					}
					break;
				// 5 - Talk with Tailoring Master Daphnis
				case 203793:
					if (var == 4)
					{
						switch (dialogActionId)
						{
							case DialogAction.QUEST_SELECT:
								return SendQuestDialog(env, 2375);
							case DialogAction.SETPRO7:
								return DefaultCloseDialog(env, 4, 7, 152201632, 1, 0, 0);
						}
					}
					break;
				// 6 - Talk with Cooking Master Hestia
				case 203784:
					if (var == 5)
					{
						switch (dialogActionId)
						{
							case DialogAction.QUEST_SELECT:
								return SendQuestDialog(env, 2716);
							case DialogAction.SETPRO7:
								return DefaultCloseDialog(env, 5, 7, 152201644, 1, 0, 0);
						}
					}
					break;
				// 7 - Talk with Alchemy Master Diana
				case 203786:
					if (var == 6)
					{
						switch (dialogActionId)
						{
							case DialogAction.QUEST_SELECT:
								return SendQuestDialog(env, 3057);
							case DialogAction.SETPRO7:
								return DefaultCloseDialog(env, 6, 7, 152201643, 1, 0, 0);
						}
					}
					break;
				// 8 - Talk with Crafting Master Anusis
				case 798316:
					if (var == 7)
					{
						switch (dialogActionId)
						{
							case DialogAction.QUEST_SELECT:
								return SendQuestDialog(env, 3398);
							case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
								return CheckItemExistence(env, 7, 8, false, 186000077, 1, true, 10000, 10001, 0, 0);
						}
					}
					break;
				// 10 - Take the Glossy Oath Stone to High Priest Jucleas and ask him to perform the ritual of affirmation
				case 203752:
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (var == 8)
							{
								return SendQuestDialog(env, 3739);
							}
							return false;
						case DialogAction.SET_SUCCEED:
							if (player.GetInventory().GetItemCountByItemId(186000081) >= 1)
							{
								RemoveQuestItem(env, 186000081, 1);
								return DefaultCloseDialog(env, 8, 8, true, false);
							}
							else
							{
								return SendQuestDialog(env, 3825);
							}
						case DialogAction.FINISH_DIALOG:
							return SendQuestSelectionDialog(env);
					}
					break;
				// No match
				default:
					return SendQuestStartDialog(env);
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 203701)
			{
				if (dialogActionId == DialogAction.USE_OBJECT)
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
}
