using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/linkgateFoundry/LinkgateFoundrySecretTeleportAI (@author Сheatkiller).</summary>
[AIName("linkgateFoundrySecretRoomTeleport")]
public class LinkgateFoundrySecretTeleportAI : ActionItemNpcAI
{
    public LinkgateFoundrySecretTeleportAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleUseItemFinish(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        SwitchFloor(player, dialogActionId);
        return true;
    }

    private void SwitchFloor(Player player, int dialogActionId)
    {
        switch (dialogActionId)
        {
            case DialogAction.SETPRO1:
                TeleportService.TeleportTo(player, 301270000, 177.65f, 258.15f, 313, (byte)60, TeleportAnimation.FADE_OUT_BEAM);
                break;
            case DialogAction.SETPRO2:
                TeleportService.TeleportTo(player, 301270000, 176.11f, 258.44f, 354, (byte)60, TeleportAnimation.FADE_OUT_BEAM);
                break;
            case DialogAction.SETPRO3:
                TeleportService.TeleportTo(player, 301270000, 176.11f, 258.44f, 394, (byte)60, TeleportAnimation.FADE_OUT_BEAM);
                break;
        }
    }
}
