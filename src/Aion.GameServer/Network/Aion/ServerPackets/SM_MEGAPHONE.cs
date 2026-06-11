using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_MEGAPHONE (Artur, Neon). -megaphone chat window message. Nested FactionLabel enum carries a byte id derived from Race.getRaceId() (not ordinal) -> sealed value-class with static readonly instances + public id field. Race/Player red-tolerated.</summary>
public class SM_MEGAPHONE : AionServerPacket
{
    private FactionLabel senderFaction;
    private string senderName;
    private string message;
    private int itemId;

    public SM_MEGAPHONE(Player sender, string message, int itemId)
        : this(sender.GetRace() == Race.ELYOS ? FactionLabel.ELYOS : FactionLabel.ASMODIANS, sender.GetName(), message, itemId)
    {
    }

    public SM_MEGAPHONE(FactionLabel senderFaction, string senderName, string message, int itemId)
    {
        this.senderFaction = senderFaction;
        this.senderName = senderName;
        this.message = message;
        this.itemId = itemId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteS(senderName);
        WriteS(message);
        WriteD(itemId); // used by the client to determine message color
        WriteC(senderFaction.id);
    }

    public sealed class FactionLabel
    {
        public static readonly FactionLabel NONE = new FactionLabel((byte)-1);
        public static readonly FactionLabel ELYOS = new FactionLabel((byte)Race.ELYOS.GetRaceId());
        public static readonly FactionLabel ASMODIANS = new FactionLabel((byte)Race.ASMODIANS.GetRaceId());

        public readonly byte id;

        private FactionLabel(byte id)
        {
            this.id = id;
        }
    }
}
