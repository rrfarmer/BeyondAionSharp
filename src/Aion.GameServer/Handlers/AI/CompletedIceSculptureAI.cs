using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/events/iceFestival/CompletedIceSculptureAI.</summary>
[AIName("icefestival_completed_ice_sculpture")]
public class CompletedIceSculptureAI : GeneralNpcAI
{
    public CompletedIceSculptureAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        // custom dialog handling because the quest doesn't show up in the quest selection (incomplete quest data in the 4.8 game client)
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetOwner().GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        switch (dialogActionId)
        {
            case DialogAction.SETPRO1:
                Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 22719, 1, player).UseWithoutPropSkill(); // world_event_icebuff_drop
                PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetOwner().GetObjectId(), 0));
                return true;
        }
        return false;
    }
}
