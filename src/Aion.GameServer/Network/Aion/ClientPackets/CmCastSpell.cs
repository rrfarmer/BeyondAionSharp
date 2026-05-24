using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmCastSpell : GameClientPacket
{
	public CmCastSpell(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
		ReceiveTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
	}

	public long ReceiveTimeMilliseconds { get; }

	public int SpellId { get; private set; }

	public int Level { get; private set; }

	public int TargetType { get; private set; }

	public float X { get; private set; }

	public float Y { get; private set; }

	public float Z { get; private set; }

	public int TargetObjectId { get; private set; }

	public int HitTime { get; private set; }

	public int Unknown { get; private set; }

	public IReadOnlyList<float> ExtraTargetFloats { get; private set; } = Array.Empty<float>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_CASTSPELL.readImpl.
		SpellId = buffer.ReadH();
		Level = buffer.ReadC();
		TargetType = buffer.ReadC();

		switch (TargetType)
		{
			case 0:
			case 3:
			case 4:
				TargetObjectId = buffer.ReadD();
				break;
			case 1:
				ReadPointTarget(buffer);
				break;
			case 2:
				ReadPointTarget(buffer);
				var extra = new float[8];
				for (var i = 0; i < extra.Length; i++)
					extra[i] = buffer.ReadF();
				ExtraTargetFloats = extra;
				break;
		}

		HitTime = buffer.ReadH();
		Unknown = buffer.ReadD();
	}

	private void ReadPointTarget(PacketBuffer buffer)
	{
		X = buffer.ReadF();
		Y = buffer.ReadF();
		Z = buffer.ReadF();
	}
}
