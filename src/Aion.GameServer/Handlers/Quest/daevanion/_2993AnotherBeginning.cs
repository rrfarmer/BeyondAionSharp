using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Tiger, Wakizashi, Rolandas, Pad, Neon
/// </summary>
public class _2993AnotherBeginning : AbstractQuestHandler
{
    private static readonly int[] dialogs = { 1013, 1034, 1055, 1076, 5103, 1098, 1119, 1140, 1161, 5104, 1183, 1204, 1225, 1246, 5105, 1268, 1289, 1310,
        1331, 5106, 2376, 2461, 2546, 2631, 2632 };

    private static readonly int[][] items = {
        new[] { 110600834, 110600835 }, new[] { 113600800, 113600801 }, new[] { 114600794, 114600795 }, new[] { 112600785, 112600786 },
        new[] { 111600813, 111600814 }, // Plate
        new[] { 110300881, 110300882 }, new[] { 113300860, 113300861 }, new[] { 114300893, 114300894 }, new[] { 112300784, 112300785 }, new[] { 111300834, 111300835 }, // Leather
        new[] { 110100931, 110100932 }, new[] { 113100843, 113100844 }, new[] { 114100866, 114100867 }, new[] { 112100790, 112100791 }, new[] { 111100831, 111100832 }, // Cloth
        new[] { 110500849, 110500850 }, new[] { 113500827, 113500828 }, new[] { 114500837, 114500838 }, new[] { 112500774, 112500775 }, new[] { 111500821, 111500822 }, // Chain
        new[] { 110301532, 110301543 }, new[] { 113301497, 113301509 }, new[] { 114301526, 114301538 }, new[] { 112301412, 112301421 }, new[] { 111301470, 111301480 } }; // Magical Leather

    public _2993AnotherBeginning() : base(2993)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(204076).AddOnQuestStart(questId);
        qe.RegisterQuestNpc(204076).AddOnTalkEvent(questId);
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        if (env.GetTargetId() != 204076) // Narvi
            return false;
        Player player = env.GetPlayer();
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        int dialogActionId = env.GetDialogActionId();

        if (qs == null || qs.IsStartable())
        {
            if (dialogActionId == DialogAction.EXCHANGE_COIN)
            {
                QuestService.StartQuest(env);
                return SendQuestDialog(env, 1011);
            }
            else
            {
                return SendQuestStartDialog(env);
            }
        }
        else if (qs.GetStatus() == QuestStatus.START)
        {
            if (dialogActionId == DialogAction.EXCHANGE_COIN)
                return SendQuestDialog(env, 1011);

            for (int dialogIndex = 0; dialogIndex < dialogs.Length; dialogIndex++)
            {
                if (dialogs[dialogIndex] == dialogActionId)
                {
                    foreach (int itemId in items[dialogIndex])
                    {
                        if (player.GetInventory().GetItemCountByItemId(itemId) > 0)
                        {
                            qs.SetRewardGroup(dialogIndex);
                            return SendQuestDialog(env, 1013);
                        }
                    }
                    return SendQuestDialog(env, 1352);
                }
            }

            switch (dialogActionId)
            {
                case DialogAction.SETPRO1: // get plate
                case DialogAction.SETPRO2: // get leather
                case DialogAction.SETPRO3: // get cloth
                case DialogAction.SETPRO4: // get chain
                case DialogAction.SETPRO5: // get magic leather
                    if (player.GetInventory().GetItemCountByItemId(186000041) == 0) // Daevanion's Light
                        return SendQuestDialog(env, 1009);
                    int? savedData = qs.GetRewardGroup();
                    int itemIdToRemove = 0;
                    foreach (int itemId in items[savedData.Value])
                    {
                        if (player.GetInventory().GetItemCountByItemId(itemId) > 0)
                            itemIdToRemove = itemId;
                    }
                    if (itemIdToRemove == 0)
                        return SendQuestDialog(env, 1352);
                    ChangeQuestStep(env, 0, 0, true);
                    RemoveQuestItem(env, 186000041, 1);
                    RemoveQuestItem(env, itemIdToRemove, 1);
                    qs.SetRewardGroup(dialogActionId - DialogAction.SETPRO1); // 0 - 4
                    return SendQuestDialog(env, DialogPageExtensions.GetRewardPageByIndex(qs.GetRewardGroup()).Id());
            }
            return base.OnDialogEvent(env);
        }
        else if (qs.GetStatus() == QuestStatus.REWARD)
        {
            return SendQuestEndDialog(env);
        }
        return false;
    }
}
