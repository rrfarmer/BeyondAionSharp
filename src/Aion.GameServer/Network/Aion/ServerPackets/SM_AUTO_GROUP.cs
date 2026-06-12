using System;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_AUTO_GROUP (SheppeR, Guapo, nrg, Estrayl). Auto-group/instance matchmaking UI packet keyed by windowId (request/waiting/pass/enter/icon windows). Converges AutoGroupUtility SM_AUTO_GROUP ctors. switch-arrow grouped cases->stacked case labels; AutoGroupType.getAGTByMaskId->GetAGTByMaskId; IllegalArgument->Argument. AionServerPacket/AutoGroupType/write* red-tolerated.</summary>
public class SM_AUTO_GROUP : AionServerPacket
{
    public const byte WND_ENTRY_ICON = 6;
    private readonly int maskId;
    private readonly int mapId;
    private readonly int messageId;
    private readonly int titleId;
    private int windowId;
    private int requestTypeId;
    private bool close;
    private string name = "";

    public SM_AUTO_GROUP(int maskId)
    {
        AutoGroupType agt = AutoGroupTypeExtensions.GetAGTByMaskId(maskId).Value;
        if (agt == null)
        {
            throw new ArgumentException("AutoGroupType not found for maskId: " + maskId);
        }

        this.maskId = maskId;
        this.messageId = agt.GetL10nId();
        this.titleId = agt.GetTemplate().GetTitleId();
        this.mapId = agt.GetTemplate().GetInstanceMapId();
    }

    public SM_AUTO_GROUP(int maskId, int windowId)
        : this(maskId)
    {
        this.windowId = windowId;
    }

    public SM_AUTO_GROUP(int maskId, int windowId, bool close)
        : this(maskId)
    {
        this.windowId = windowId;
        this.close = close;
    }

    public SM_AUTO_GROUP(int maskId, int windowId, int requestTypeId, string name)
        : this(maskId)
    {
        this.windowId = windowId;
        this.requestTypeId = requestTypeId;
        this.name = name;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(maskId);
        WriteC(windowId);
        WriteD(mapId);
        switch (windowId)
        {
            case 0:
            case 7: // 0 = request entry, 7 = failed window
                WriteD(messageId);
                WriteD(titleId);
                WriteD(0);
                break;
            case 1:
            case 3:
            case 8: // 1 = waiting window, 3 = pass window, 8 = on login
                WriteD(0); // progression type: 0 = Group Formation in Progress, 1 = Opponent Group formation in progress
                WriteD(0);
                WriteD(requestTypeId);
                break;
            case 2:
            case 4:
            case 5: // 2 = cancel looking, 4 = enter window, 5 = after clicking enter
                WriteD(0);
                WriteD(0);
                WriteD(0);
                break;
            case WND_ENTRY_ICON: // entry icon
                WriteD(messageId);
                WriteD(titleId);
                WriteD(close ? 0 : 1);
                break;
        }
        WriteC(0);
        WriteS(name);
    }
}
