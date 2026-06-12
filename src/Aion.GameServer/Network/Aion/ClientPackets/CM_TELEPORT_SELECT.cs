using System.Collections.Generic;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Teleport;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_TELEPORT_SELECT (ATracer, orz, KID). Teleports the player to a selected location via a teleporter NPC. TeleportService/TeleporterTemplate red-tolerated.</summary>
public class CM_TELEPORT_SELECT : AionClientPacket
{
    /// <summary>NPC object ID</summary>
    private int targetObjId;

    /// <summary>Destination of teleport</summary>
    private int locId;

    public CM_TELEPORT_SELECT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetObjId = ReadD();
        locId = ReadD(); // locationId
        ReadH();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player.IsDead())
            return;

        AionObject obj = player.GetKnownList().GetObject(targetObjId);
        if (!(obj is Npc npc))
        {
            if (obj == null)
                obj = global::Aion.GameServer.World.World.GetInstance().FindVisibleObject(targetObjId);
            AuditLogger.Log(player, "tried to teleport to locId " + locId + " via " + (obj == null ? "unknown npc (objId " + targetObjId + ")" : obj)
                + " at " + player.GetPosition());
            return;
        }
        TeleporterTemplate template = TeleportService.ValidateTeleporterAndGetTemplate(player, npc);
        if (template == null)
            return;
        TeleportLocation location = template.GetTeleLocIdData().GetTeleportLocation(locId);
        if (location == null)
        {
            AuditLogger.Log(player, "tried to teleport to invalid locId " + locId + " via " + npc + " at " + player.GetPosition());
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE());
            return;
        }
        TeleportService.Teleport(player, location, npc.HasStatic() ? TeleportAnimation.JUMP_IN_STATUE : TeleportAnimation.JUMP_IN);
    }
}
