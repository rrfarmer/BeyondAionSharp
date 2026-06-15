using Aion.GameServer.Ai;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/portals/StudioPortalAI (Rolandas).</summary>
[AIName("studioportal")]
public class StudioPortalAI : ActionItemNpcAI
{
    public StudioPortalAI(Npc owner)
        : base(owner)
    {
    }

    public override bool OnDialogSelect(Player player, int dialogActionId, int questId, int extendedRewardIndex)
    {
        return true;
    }

    protected override void HandleUseItemFinish(Player player)
    {
        WorldMapInstance instance;
        float x, y, z;
        byte heading = 0;
        WorldMapType? mapType = WorldMapTypeExtensions.GetWorld(player.GetWorldId());
        if (mapType == WorldMapType.HOUSING_IDLF_PERSONAL || mapType == WorldMapType.HOUSING_IDDF_PERSONAL)
        { // leaving studio
            House studio = HousingService.GetInstance().GetPlayerStudio(player.GetPosition().GetWorldMapInstance().GetOwnerId());
            if (studio == null) // should not happen unless this instance was custom spawned by admin
                return;
            instance = World.World.GetInstance().GetWorldMap(studio.GetAddress().GetExitMapId().Value).GetMainWorldMapInstance();
            x = studio.GetAddress().GetExitX().Value;
            y = studio.GetAddress().GetExitY().Value;
            z = studio.GetAddress().GetExitZ().Value;
        }
        else
        { // entering own studio
            House studio = HousingService.GetInstance().GetPlayerStudio(player.GetObjectId());
            if (studio == null)
            { // doesn't own studio
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_HOUSING_ENTER_NEED_HOUSE());
                return;
            }
            instance = InstanceService.GetOrCreateHouseInstance(studio);
            x = studio.GetAddress().GetX();
            y = studio.GetAddress().GetY();
            z = studio.GetAddress().GetZ();
            heading = (byte)studio.GetTeleportHeading();
        }
        TeleportService.TeleportTo(player, instance, x, y, z, heading, TeleportAnimation.FADE_OUT_BEAM);
    }
}
