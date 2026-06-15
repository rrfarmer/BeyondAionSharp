using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Thuatan, vlog, Pad
/// </summary>
public class _29038MasterCooksPotential : AbstractQuestHandler
{
	public _29038MasterCooksPotential() : base(29038)
	{
	}

	public override void Register()
	{
		qe.RegisterQuestNpc(204100).AddOnQuestStart(questId);
		qe.RegisterQuestNpc(204100).AddOnTalkEvent(questId);
		qe.RegisterQuestNpc(204101).AddOnTalkEvent(questId);
		qe.RegisterOnFailCraft(182207907, questId);
		qe.RegisterOnFailCraft(182207908, questId);
		qe.RegisterOnFailCraft(182207909, questId);
		qe.RegisterOnFailCraft(182207910, questId);
	}

	public override bool OnDialogEvent(QuestEnv env)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		int dialogActionId = env.GetDialogActionId();
		int targetId = env.GetTargetId();

		if (dialogActionId == DialogAction.QUEST_SELECT && !Aion.GameServer.Services.Craft.CraftSkillUpdateService.GetInstance().CanLearnMoreMasterCraftingSkill(player))
		{
			return SendQuestSelectionDialog(env);
		}

		if (qs == null || qs.IsStartable())
		{
			if (targetId == 204100) // Lainita
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
			switch (targetId)
			{
				case 204101: // Daraia
					long kinah = player.GetInventory().GetKinah();
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							if (kinah >= 6500)
							{
								switch (var)
								{
									case 0:
									{
										return SendQuestDialog(env, 1011);
									}
									case 3:
									{
										return SendQuestDialog(env, 1352);
									}
									case 6:
									{
										return SendQuestDialog(env, 1693);
									}
									case 9:
									{
										return SendQuestDialog(env, 2034);
									}
									case 1:
									{
										if (!player.GetRecipeList().IsRecipePresent(155007241))
											return SendQuestDialog(env, 4081);
										return false;
									}
									case 4:
									{
										if (!player.GetRecipeList().IsRecipePresent(155007242))
											return SendQuestDialog(env, 4166);
										return false;
									}
									case 7:
									{
										if (!player.GetRecipeList().IsRecipePresent(155007243))
											return SendQuestDialog(env, 4251);
										return false;
									}
									case 10:
									{
										if (!player.GetRecipeList().IsRecipePresent(155007244))
											return SendQuestDialog(env, 4336);
										return false;
									}
								}
								return false;
							}
							else
							{
								return SendQuestDialog(env, 4400);
							}
						case DialogAction.SETPRO10:
							player.GetInventory().DecreaseKinah(6500);
							if (var == 0)
							{
								return DefaultCloseDialog(env, 0, 2, 152207202, 1, 0, 0); // 2
							}
							else if (var == 1)
							{
								return DefaultCloseDialog(env, 1, 2, 152207202, 1, 0, 0); // 2
							}
							return false;
						case DialogAction.SETPRO20:
							player.GetInventory().DecreaseKinah(6500);
							if (var == 3)
							{
								return DefaultCloseDialog(env, 3, 5, 152207203, 1, 0, 0); // 5
							}
							else if (var == 4)
							{
								return DefaultCloseDialog(env, 4, 5, 152207203, 1, 0, 0); // 5
							}
							return false;
						case DialogAction.SETPRO30:
							player.GetInventory().DecreaseKinah(6500);
							if (var == 6)
							{
								return DefaultCloseDialog(env, 6, 8, 152207204, 1, 0, 0); // 8
							}
							else if (var == 7)
							{
								return DefaultCloseDialog(env, 7, 8, 152207204, 1, 0, 0); // 8
							}
							return false;
						case DialogAction.SETPRO40:
							player.GetInventory().DecreaseKinah(6500);
							if (var == 9)
							{
								return DefaultCloseDialog(env, 9, 11, 152207205, 1, 0, 0); // 11
							}
							else if (var == 10)
							{
								return DefaultCloseDialog(env, 10, 11, 152207205, 1, 0, 0); // 11
							}
							return false;
						case DialogAction.FINISH_DIALOG:
							return SendQuestSelectionDialog(env);
					}
					break;
				case 204100: // Lainita
					switch (dialogActionId)
					{
						case DialogAction.QUEST_SELECT:
							switch (var)
							{
								case 2:
								{
									return SendQuestDialog(env, 1097);
								}
								case 5:
								{
									return SendQuestDialog(env, 1438);
								}
								case 8:
								{
									return SendQuestDialog(env, 1779);
								}
								case 11:
								{
									return SendQuestDialog(env, 2120);
								}
							}
							return false;
						case DialogAction.SETPRO11:
							return CheckItemExistence(env, 2, 3, false, 182207907, 1, true, 1182, 2716, 0, 0); // 3
						case DialogAction.SETPRO21:
							return CheckItemExistence(env, 5, 6, false, 182207908, 1, true, 1523, 3057, 0, 0); // 6
						case DialogAction.SETPRO31:
							return CheckItemExistence(env, 8, 9, false, 182207909, 1, true, 1864, 3398, 0, 0); // 9
						case DialogAction.SETPRO41:
							return CheckItemExistence(env, 11, 11, true, 182207910, 1, true, 5, 3057, 0, 0); // reward
						case DialogAction.FINISH_DIALOG:
							return SendQuestSelectionDialog(env);
					}
					break;
			}
		}
		else if (qs.GetStatus() == QuestStatus.REWARD)
		{
			if (targetId == 204100) // Lainita
			{
				return SendQuestEndDialog(env);
			}
		}
		return false;
	}

	public override bool OnFailCraftEvent(QuestEnv env, int itemId)
	{
		Player player = env.GetPlayer();
		QuestState qs = player.GetQuestStateList().GetQuestState(questId);
		if (qs != null && qs.GetStatus() == QuestStatus.START)
		{
			switch (itemId)
			{
				case 182207907:
					ChangeQuestStep(env, 2, 1); // 1
					return true;
				case 182207908:
					ChangeQuestStep(env, 5, 4); // 4
					return true;
				case 182207909:
					ChangeQuestStep(env, 8, 7); // 7
					return true;
				case 182207910:
					ChangeQuestStep(env, 11, 10); // 10
					return true;
			}
		}
		return false;
	}
}
