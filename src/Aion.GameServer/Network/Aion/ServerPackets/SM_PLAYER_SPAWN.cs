using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PLAYER_SPAWN (-Nemesiss-). Tells the client which map/channel to load for the entering player (personal-instance negative channel, position/heading, beginner-twin flag). Converges PlayerEnterWorldService. WorldMapType/World/AionServerPacket red-tolerated.</summary>
public class SM_PLAYER_SPAWN : AionServerPacket
{
    /// <summary>Player that is entering game.</summary>
    private readonly Player player;

    public SM_PLAYER_SPAWN(Player player)
    {
        this.player = player;
    }

    protected override void WriteImpl(AionConnection con)
    {
        bool isPersonal = WorldMapType.GetWorld(player.GetWorldId()).IsPersonal();
        int worldChannel = player.GetWorldId() + player.GetInstanceId() - 1;
        WriteD(isPersonal ? -worldChannel : worldChannel); // world + chnl
        WriteD(player.GetWorldId());
        WriteD(0x00); // unk
        WriteC(isPersonal ? 1 : 0);
        WriteF(player.GetX()); // x
        WriteF(player.GetY()); // y
        WriteF(player.GetZ()); // z
        WriteC(player.GetHeading()); // heading
        WriteD(0); // new 2.5
        WriteD(0); // new 2.5
        // 1 - Azphels Curse (no longer implemented since removal of serial killer system in 4.8)
        // 2-6 - Victorys Pledge tiers; 7-12 - Boost Moral arena/battlefield buffs
        WriteD(0);
        if (World.GetInstance().GetWorldMap(player.GetWorldId()).GetTemplate().GetBeginnerTwinCount() > 0)
            WriteC(1);
        else
            WriteC(0);
        WriteD(0); // 4.0
        WriteC(0); // 4.7
    }
}
