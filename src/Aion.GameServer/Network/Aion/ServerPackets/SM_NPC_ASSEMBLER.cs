using Aion.GameServer.Model.Assemblednpc;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_NPC_ASSEMBLER (xTz). Spawns/despawns an assembled NPC (e.g. dredgion carrier): per-part route/object/npc/static ids + time on map, or 0 for despawn. Converges BalaurAssaultService/FortressAssault SM_NPC_ASSEMBLER. AssembledNpc/AssembledNpcPart/AionServerPacket red-tolerated.</summary>
public class SM_NPC_ASSEMBLER : AionServerPacket
{
    private AssembledNpc assembledNpc;
    private int routeId;
    private long timeOnMap;

    public SM_NPC_ASSEMBLER(AssembledNpc assembledNpc)
    {
        this.assembledNpc = assembledNpc;
        if (assembledNpc != null)
        {
            this.routeId = assembledNpc.GetRouteId();
            timeOnMap = assembledNpc.GetTimeOnMap();
        }
    }

    protected override void WriteImpl(AionConnection con)
    {
        if (assembledNpc != null)
        {
            WriteD(assembledNpc.GetAssembledParts().Count); // size
            foreach (AssembledNpcPart npc in assembledNpc.GetAssembledParts())
            {
                WriteD(routeId); // routeId
                WriteD(npc.GetObject()); // objectId
                WriteD(npc.GetNpcId()); // npc Id
                WriteD(npc.GetStaticId()); // static Id
                WriteQ(timeOnMap); // time
            }
        }
        else
        {
            WriteD(0);
        }
    }
}
