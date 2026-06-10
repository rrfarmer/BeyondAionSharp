using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GATHERABLE_INFO (ATracer). Sends a gatherable/static-object's position, ids, open/closed door state, heading, and l10n. instanceof StaticDoor->is StaticDoor. VisibleObject/StaticDoor/AionServerPacket red-tolerated.</summary>
public class SM_GATHERABLE_INFO : AionServerPacket
{
    private VisibleObject visibleObject;

    public SM_GATHERABLE_INFO(VisibleObject visibleObject)
    {
        this.visibleObject = visibleObject;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteF(visibleObject.GetX());
        WriteF(visibleObject.GetY());
        WriteF(visibleObject.GetZ());
        WriteD(visibleObject.GetObjectId());
        WriteD(visibleObject.GetSpawn().GetStaticId());
        WriteD(visibleObject.GetObjectTemplate().GetTemplateId());
        if (visibleObject is StaticDoor)
        {
            if (((StaticDoor)visibleObject).IsOpen())
            {
                WriteH(0x09);
            }
            else
            {
                WriteH(0x0A);
            }
        }
        else
        {
            WriteH(1);
        }
        WriteC(visibleObject.GetSpawn().GetHeading());
        WriteD(visibleObject.GetObjectTemplate().GetL10nId());
        WriteH(0);
        WriteH(0);
        WriteH(0);
        WriteC(100); // unk
    }
}
