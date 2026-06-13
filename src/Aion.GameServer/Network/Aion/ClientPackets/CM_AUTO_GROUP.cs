using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Autogroup;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Utils;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_AUTO_GROUP (Shepper, Guapo, nrg). Auto-group window actions (100 start / 101 cancel-reg / 102 enter / 103 cancel-enter / 104 icon). EntryRequestType/AutoGroupService/PeriodicInstanceManager red-tolerated.</summary>
public class CM_AUTO_GROUP : AionClientPacket
{
    private int instanceMaskId;
    private byte windowId;
    private byte entryRequestId;

    public CM_AUTO_GROUP(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        instanceMaskId = ReadD();
        windowId = ReadC();
        entryRequestId = ReadC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (!AutoGroupConfig.AUTO_GROUP_ENABLE)
        {
            PacketSendUtility.SendMessage(player, "Auto Group is disabled");
            return;
        }
        switch (windowId)
        {
            case 100:
                EntryRequestType? ert = EntryRequestTypeExtensions.GetTypeById((sbyte)entryRequestId);
                if (ert == null)
                {
                    return;
                }
                AutoGroupService.GetInstance().StartLooking(player, instanceMaskId, ert.Value);
                break;
            case 101:
                AutoGroupService.GetInstance().CancelRegistration(player, instanceMaskId);
                break;
            case 102:
                AutoGroupService.GetInstance().PressEnter(player, instanceMaskId);
                break;
            case 103:
                AutoGroupService.GetInstance().CancelEnter(player, instanceMaskId);
                break;
            case 104:
                // is sent if a player clicks the icon
                PeriodicInstanceManager.GetInstance().HandleRequest(player, instanceMaskId);
                break;
            case 105:
                // DredgionRegService.getInstance().failedEnterDredgion(player);
                break;
        }
    }
}
