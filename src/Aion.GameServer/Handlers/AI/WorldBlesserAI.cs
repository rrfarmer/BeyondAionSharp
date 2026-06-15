using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/worlds/WorldBlesserAI (@author xTz, bobobear).</summary>
[AIName("world_blesser")]
public class WorldBlesserAI : GeneralNpcAI
{
    public WorldBlesserAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        switch (GetNpcId())
        {
            case 831030: // Netalion
            case 831024: // Renniah
            case 831025: // Erdat
            case 831026: // Erdat
            case 831028: // Erdat
            case 831027: // Karzanke
                base.HandleDialogStart(player);
                break;
            default:
                {
                    PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
                    break;
                }
        }
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        QuestEnv env = new QuestEnv(GetOwner(), player, questId, dialogActionId);
        env.SetExtendedRewardIndex(extendedRewardIndex);
        if (Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnDialog(env) && dialogActionId != DialogAction.SETPRO1)
        {
            return true;
        }
        if (dialogActionId == DialogAction.SETPRO1)
        {
            // int chance = Rnd.get(1, 2);
            // 951: Blessing of Health I, 955: Blessing of Rock I : 3.9
            // 20950 : Blessing of Growth : 4.0
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20950, 1, player).UseWithoutPropSkill();
        }
        else if (dialogActionId == DialogAction.QUEST_SELECT && questId != 0)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 10, questId));
        }
        return true;
    }
}
