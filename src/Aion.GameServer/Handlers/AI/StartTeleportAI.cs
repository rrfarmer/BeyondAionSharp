using Aion.GameServer.Ai;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/danuarSanctuary/StartTeleportAI (@author Cheatkiller).</summary>
[AIName("dsiportal")]
public class StartTeleportAI : NpcAI
{
    public StartTeleportAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleDialogStart(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 1011));
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        SwitchWay(player);
        return true;
    }

    private void SwitchWay(Player player)
    {
        Item key = null;
        float x = 0;
        float y = 0;
        float z = 0;
        byte h = 0;
        switch (GetOwner().GetNpcId())
        {
            case 701871:
                key = player.GetInventory().GetFirstItemByItemId(185000183);
                x = 1003.27f;
                y = 1371.26f;
                z = 338;
                h = 101;
                break;
            case 701872:
                key = player.GetInventory().GetFirstItemByItemId(185000182);
                x = 716.89f;
                y = 980.95f;
                z = 318.71f;
                h = 108;
                break;
            case 701873:
                key = player.GetInventory().GetFirstItemByItemId(185000181);
                x = 1014.81f;
                y = 275.87f;
                z = 310.47f;
                h = 0;
                break;
        }

        if (key != null)
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), 0));
            TeleportService.TeleportTo(player, player.GetWorldId(), player.GetInstanceId(), x, y, z, h, TeleportAnimation.FADE_OUT_BEAM);
        }
        else
        {
            PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.NO_RIGHT.Id()));
        }
    }
}
