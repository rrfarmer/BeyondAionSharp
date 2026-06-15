using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// Quest starter: Lavirintos (203701). Collect the amulets (600) from Calydon Chasers and Calydon Shamans and take them Lavirintos. Defeat Great
/// Protectors in the Eye of Reshanta (300): Aether's Defender (251002), Fire's Defender (251021), Ancient Defender (251018), Nature's Defender
/// (251039), Light's Defender (251033), Shadow's Defender (251036). Talk with Lavirintos. Go to the Dredgion and kill a Dredgion Captains (1): Captain
/// Adhati (214823), Captain Mituna (216850). Talk with Lavirintos. Fill yourself with Divine Power and take Divine Hoat Stone (186000082) to High
/// Priest Jucleas (203752) for the final blessing ritual. Talk with Lavirintos.
/// @author vlog, bobobear
/// </summary>
public class _3940Loyalty : AbstractQuestHandler
{
    private static readonly int[] npcs = { 203701, 203752 };
    private static readonly int[] mobs = { 251002, 251021, 251018, 251039, 251033, 251036, 214823, 216850 };

    public _3940Loyalty() : base(3940)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203701).AddOnQuestStart(questId);
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
            if (targetId == 203701)
            { // Lavirintos
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

        if (qs == null)
            return false;

        int var = qs.GetQuestVars().GetQuestVars();

        if (qs.GetStatus() == QuestStatus.START)
        {
            switch (targetId)
            {
                case 203701: // Lavirintos
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
                            return CheckQuestItems(env, 0, 6, false, 10000, 10001); // 1
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
                case 203752: // Jucleas
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
                                return CheckItemExistence(env, 5, 5, false, 186000083, 1, true, 2718, 2887, 0, 0);
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
            if (targetId == 203701)
            { // Lavirintos
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
