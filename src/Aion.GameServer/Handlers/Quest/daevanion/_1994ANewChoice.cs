using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;

namespace Aion.GameServer.Handlers.Quest;

/// <summary>
/// @author Tiger, Wakizashi, Rolandas, Pad, Neon
/// </summary>
public class _1994ANewChoice : AbstractQuestHandler
{
    private static readonly int[] dialogs = { 1013, 1034, 1055, 1076, 5103, 1098, 1119, 1140, 1161, 1204, 1225, 1246, 5105, 1183 };
    private static readonly int[][] items =
    {
        new[] { 100000723, 100000724 }, new[] { 100900554, 100900555 }, new[] { 101300538, 101300539 }, new[] { 100200673, 100200674 },
        new[] { 101700594, 101700595 }, // physical
        new[] { 100100568, 100100569 }, new[] { 101500566, 101500567 }, new[] { 100600608, 100600609 }, new[] { 100500572, 100500573 }, new[] { 101800569, 101800570 }, // magical
        new[] { 101900562, 101900563 }, new[] { 102000592, 102000593 }, new[] { 102100517, 102100518 }, new[] { 115000826, 115000828 }
    }; // shield

    public _1994ANewChoice() : base(1994)
    {
    }

    public override void Register()
    {
        qe.RegisterQuestNpc(203754).AddOnQuestStart(questId); // Aithra
        qe.RegisterQuestNpc(203754).AddOnTalkEvent(questId); // Aithra
    }

    public override bool OnDialogEvent(QuestEnv env)
    {
        if (env.GetTargetId() != 203754) // Aithra
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
                case DialogAction.SETPRO1:
                case DialogAction.SETPRO2:
                case DialogAction.SETPRO3:
                case DialogAction.SETPRO4:
                case DialogAction.SETPRO5:
                case DialogAction.SETPRO6:
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
                    qs.SetRewardGroup(dialogActionId - DialogAction.SETPRO1); // 0 - 5
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
