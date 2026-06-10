using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Clientpackets;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_PLASTIC_SURGERY (IlBuono). Plastic-surgery/gender-switch prompt (objId + has-ticket + gender-switch). CM_CHARACTER_EDIT/Player red-tolerated.</summary>
public class SM_PLASTIC_SURGERY : AionServerPacket
{
    private int playerObjId;
    private bool hasTicket;
    private bool isGenderSwitch;

    public SM_PLASTIC_SURGERY(Player player, bool isGenderSwitch)
    {
        this.playerObjId = player.GetObjectId();
        this.hasTicket = CM_CHARACTER_EDIT.CheckOrRemoveTicket(player, isGenderSwitch, false);
        this.isGenderSwitch = isGenderSwitch;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(playerObjId);
        WriteC(hasTicket ? 1 : 2);
        WriteC(isGenderSwitch ? 1 : 0);
    }
}
