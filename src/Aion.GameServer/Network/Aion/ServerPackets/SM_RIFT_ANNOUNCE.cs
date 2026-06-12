using System.Collections.Generic;
using Aion.GameServer.Controllers;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_RIFT_ANNOUNCE (Sweetkr, -Enomine-). Rift map announce (12-slot counts), silentera (gelk/ingg), rift spawn master/slave info, and despawn, keyed by actionId. Converges RiftInformer SM_RIFT_ANNOUNCE ctors. Map->Dictionary; rifts.values()->Values; switch-on-actionId; writeRiftType helper. RVController/AionServerPacket/write* red-tolerated.</summary>
public class SM_RIFT_ANNOUNCE : AionServerPacket
{
    private int actionId;
    private RVController rift;
    private Dictionary<int, int> rifts;
    private int objectId;
    private int gelkmaros, inggison;

    public SM_RIFT_ANNOUNCE(Dictionary<int, int> rifts)
    {
        this.actionId = 0;
        this.rifts = rifts;
    }

    public SM_RIFT_ANNOUNCE(bool gelkmaros, bool inggison)
    {
        this.gelkmaros = gelkmaros ? 1 : 0;
        this.inggison = inggison ? 1 : 0;
        this.actionId = 1;
    }

    public SM_RIFT_ANNOUNCE(RVController rift, bool isMaster)
    {
        this.rift = rift;
        this.actionId = isMaster ? 2 : 3;
    }

    public SM_RIFT_ANNOUNCE(int objectId)
    {
        this.objectId = objectId;
        this.actionId = 4;
    }

    protected override void WriteImpl(AionConnection con)
    {
        switch (actionId)
        {
            case 0: // announce
                WriteH(1 + (rifts.Values.Count * 4)); // following byte length
                WriteC(actionId);
                foreach (int value in rifts.Values)
                    WriteD(value);
                break;
            case 1: // silentera
                WriteH(9); // following byte length
                WriteC(actionId);
                WriteD(gelkmaros);
                WriteD(inggison);
                break;
            case 2:
                WriteH(35); // following byte length
                WriteC(actionId);
                WriteD(rift.GetOwner().GetObjectId());
                WriteD(rift.GetMaxEntries().Value);
                WriteD(rift.GetRemainTime());
                WriteD(rift.GetMinLevel().Value);
                WriteD(rift.GetMaxLevel().Value);
                WriteF(rift.GetOwner().GetX());
                WriteF(rift.GetOwner().GetY());
                WriteF(rift.GetOwner().GetZ());
                WriteRiftType();
                WriteC(rift.IsMaster() ? 1 : 0); // display | hide
                break;
            case 3:
                WriteH(15); // following byte length
                WriteC(actionId);
                WriteD(rift.GetOwner().GetObjectId());
                WriteD(rift.GetUsedEntries());
                WriteD(rift.GetRemainTime());
                WriteRiftType();
                WriteC(0); // unk
                break;
            case 4: // rift despawn
                WriteH(5); // following byte length
                WriteC(actionId);
                WriteD(objectId);
                break;
        }
    }

    private void WriteRiftType()
    {
        // 1 vortex, 2, concert hall, 3 pangaea, 4 chaos rift, 5 infiltration rift
        if (rift.IsVortex())
        {
            WriteC(1);
        }
        else if (rift.IsVolatile())
        {
            WriteC(4);
        }
        else if (rift.IsInvasion())
        {
            WriteC(5);
        }
        else
        {
            WriteC(0);
        }
    }
}
