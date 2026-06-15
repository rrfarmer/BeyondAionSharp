using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.Quest
{
    /// <summary>
    /// @author kecimis
    /// </summary>
    public class _2990MakingTheDaevanionWeapon : AbstractQuestHandler
    {
        private static readonly int[] npc_ids = { 204146 };

        public _2990MakingTheDaevanionWeapon() : base(2990)
        {
        }

        public override void Register()
        {
            qe.RegisterQuestNpc(204146).AddOnQuestStart(questId); // Kanensa
            qe.RegisterQuestNpc(256617).AddOnKillEvent(questId); // Strange Lake Spirit
            qe.RegisterQuestNpc(253720).AddOnKillEvent(questId); // Lava Hoverstone
            qe.RegisterQuestNpc(254513).AddOnKillEvent(questId); // Disturbed Resident
            foreach (int npc_id in npc_ids)
                qe.RegisterQuestNpc(npc_id).AddOnTalkEvent(questId);
        }

        public override bool OnDialogEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();

            int targetId = 0;
            if (env.GetVisibleObject() is Npc)
                targetId = ((Npc)env.GetVisibleObject()).GetNpcId();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);

            if (qs == null || qs.IsStartable())
            {
                if (targetId == 204146) // Kanensa
                {
                    if (env.GetDialogActionId() == DialogAction.QUEST_SELECT)
                    {
                        int plate = player.GetEquipment().ItemSetPartsEquipped(9);
                        int chain = player.GetEquipment().ItemSetPartsEquipped(8);
                        int leather = player.GetEquipment().ItemSetPartsEquipped(7);
                        int cloth = player.GetEquipment().ItemSetPartsEquipped(6);
                        int gunner = player.GetEquipment().ItemSetPartsEquipped(378);

                        if (plate != 5 && chain != 5 && leather != 5 && cloth != 5 && gunner != 5)
                            return SendQuestDialog(env, 4848);
                        else
                            return SendQuestDialog(env, 4762);
                    }
                    else
                        return SendQuestStartDialog(env);
                }
            }

            if (qs == null)
                return false;

            int var = qs.GetQuestVarById(0);
            int var1 = qs.GetQuestVarById(1);

            if (qs.GetStatus() == QuestStatus.START)
            {
                if (targetId == 204146)
                {
                    switch (env.GetDialogActionId())
                    {
                        case DialogAction.QUEST_SELECT:
                            if (var == 0)
                                return SendQuestDialog(env, 1011);
                            if (var == 1)
                                return SendQuestDialog(env, 1352);
                            if (var == 2 && var1 == 60)
                                return SendQuestDialog(env, 1693);
                            if (var == 3 && player.GetInventory().GetItemCountByItemId(186000040) > 0)
                                return SendQuestDialog(env, 2034);
                            return false;
                        case DialogAction.CHECK_USER_HAS_QUEST_ITEM:
                            if (var == 0)
                            {
                                if (QuestService.CollectItemCheck(env, true))
                                {
                                    qs.SetQuestVarById(0, var + 1);
                                    UpdateQuestStatus(env);
                                    return SendQuestDialog(env, 10000);
                                }
                                else
                                    return SendQuestDialog(env, 10001);
                            }
                            break;
                        case DialogAction.SELECT2:
                            if (var == 0)
                                return SendQuestDialog(env, 1352);
                            return false;
                        case DialogAction.SELECT4_1:
                            if (var == 3)
                            {
                                if (player.GetCommonData().GetDp() == 4000 && player.GetInventory().GetItemCountByItemId(186000040) > 0)
                                {
                                    RemoveQuestItem(env, 186000040, 1);
                                    player.GetCommonData().SetDp(0);
                                    qs.SetStatus(QuestStatus.REWARD);
                                    UpdateQuestStatus(env);
                                    return SendQuestDialog(env, 5);
                                }
                                else
                                    return SendQuestDialog(env, 2120);
                            }
                            break;
                        case DialogAction.SETPRO2:
                            if (var == 1)
                            {
                                qs.SetQuestVarById(0, var + 1);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                            }
                            break;
                        case DialogAction.SETPRO3:
                            if (var == 2)
                            {
                                qs.SetQuestVar(3);
                                UpdateQuestStatus(env);
                                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(env.GetVisibleObject().GetObjectId(), 10));
                                return true;
                            }
                            break;
                    }
                }
                return false;
            }
            else if (qs.GetStatus() == QuestStatus.REWARD)
            {
                if (targetId == 204146) // Kanensa
                {
                    return SendQuestEndDialog(env);
                }
                return false;
            }
            return false;
        }

        public override bool OnKillEvent(QuestEnv env)
        {
            Player player = env.GetPlayer();
            QuestState qs = player.GetQuestStateList().GetQuestState(questId);
            if (qs == null || qs.GetStatus() != QuestStatus.START)
                return false;

            int var1 = qs.GetQuestVarById(1);
            int var2 = qs.GetQuestVarById(2);
            int var3 = qs.GetQuestVarById(3);
            int targetId = env.GetTargetId();

            if ((targetId == 256617 || targetId == 253720 || targetId == 254513) && qs.GetQuestVarById(0) == 2)
            {
                switch (targetId)
                {
                    case 256617:
                        if (var1 >= 0 && var1 < 60)
                        {
                            ++var1;
                            qs.SetQuestVarById(1, var1 + 1);
                            UpdateQuestStatus(env);
                        }
                        break;
                    case 253720:
                        if (var2 >= 0 && var2 < 120)
                        {
                            ++var2;
                            qs.SetQuestVarById(2, var2 + 3);
                            UpdateQuestStatus(env);
                        }
                        break;
                    case 254513:
                        if (var3 >= 0 && var3 < 240)
                        {
                            ++var3;
                            qs.SetQuestVarById(3, var3 + 7);
                            UpdateQuestStatus(env);
                        }
                        break;
                }
            }
            if (qs.GetQuestVarById(0) == 2 && var1 == 60 && var2 == 120 && var3 == 240)
            {
                qs.SetQuestVarById(1, 60);
                UpdateQuestStatus(env);
                return true;
            }
            return false;
        }
    }
}
