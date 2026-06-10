using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CHANNEL_INFO (ATracer). Reports current channel + instance (twin) count for a position, honoring beginner instances and the fast-track emulation config. Converges PlayerEnterWorldService SM_CHANNEL_INFO. WorldPosition/WorldMapTemplate/WorldConfig/AionServerPacket red-tolerated.</summary>
public class SM_CHANNEL_INFO : AionServerPacket
{
    int instanceCount = 0;
    int currentChannel = 0;

    public SM_CHANNEL_INFO(WorldPosition position)
    {
        if (position == null || !position.IsSpawned())
        {
            instanceCount = 1;
            currentChannel = 1;
        }
        else
        {
            WorldMapTemplate template = position.GetWorldMapInstance().GetTemplate();
            if (position.GetWorldMapInstance().IsBeginnerInstance())
            {
                instanceCount = template.GetBeginnerTwinCount();
                if (WorldConfig.WORLD_EMULATE_FASTTRACK)
                    instanceCount += template.GetTwinCount();
                currentChannel = position.GetInstanceId() - 1;
            }
            else
            {
                instanceCount = template.GetTwinCount();
                if (WorldConfig.WORLD_EMULATE_FASTTRACK)
                    instanceCount += template.GetBeginnerTwinCount();
                currentChannel = position.GetInstanceId() - 1;
            }
        }
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(currentChannel);
        WriteD(instanceCount);
    }
}
