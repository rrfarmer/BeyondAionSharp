using Aion.Commons.Network;
namespace Aion.GameServer.Network.Aion.ClientPackets;
public sealed class CmLegionSendEmblemInfo : GameClientPacket {
    public CmLegionSendEmblemInfo(int opCode, IReadOnlySet<GameConnectionState> validStates) : base(opCode, validStates) {}
    public int LegionId { get; private set; }
    protected override void ReadPayload(PacketBuffer buffer) { LegionId = buffer.ReadD(); }
}
