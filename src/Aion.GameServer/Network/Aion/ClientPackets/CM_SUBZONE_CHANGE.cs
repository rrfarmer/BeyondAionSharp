using System.Collections.Generic;
using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Zone;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils;
using Aion.GameServer.World.Zone;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SUBZONE_CHANGE (Rolandas). Revalidates the player's zones on subzone transition; echoes zone info for GMs. ZoneInstance/ZoneClassName red-tolerated.</summary>
public class CM_SUBZONE_CHANGE : AionClientPacket
{
    private byte unk;

    public CM_SUBZONE_CHANGE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        // Always 1, maybe for neutral zones 0 ?
        unk = ReadC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        player.RevalidateZones();
        if (player.HasAccess(AdminConfig.ZONE_INFO))
        {
            int foundZones = 0;
            foreach (ZoneInstance zone in player.FindZones())
            {
                if (zone.GetZoneTemplate().GetZoneType() == ZoneClassName.DUMMY || zone.GetZoneTemplate().GetZoneType() == ZoneClassName.WEATHER)
                    continue;
                foundZones++;
                PacketSendUtility.SendMessage(player, "Passed zone: unk=" + unk + "; " + zone.GetZoneTemplate().GetZoneType() + " "
                    + zone.GetAreaTemplate().GetZoneName().ToString());
            }
            if (foundZones == 0)
            {
                PacketSendUtility.SendMessage(player, "Passed unknown zone, unk=" + unk);
                return;
            }
        }
    }
}
