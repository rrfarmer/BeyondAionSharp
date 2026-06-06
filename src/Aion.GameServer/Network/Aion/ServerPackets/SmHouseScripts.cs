using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseScripts : GameServerPacket
{
	public const int PacketOpCode = 131;
	private static readonly byte[] ScriptPadding = [0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD, 0xCD];

	private readonly int _houseAddress;
	private readonly IReadOnlyList<PlayerScript> _scripts;

	public SmHouseScripts(int houseAddress, PlayerScript? script)
		: this(houseAddress, script == null ? Array.Empty<PlayerScript>() : [script])
	{
	}

	public SmHouseScripts(int houseAddress, IReadOnlyList<PlayerScript> scripts)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_SCRIPTS.writeImpl.
		_houseAddress = houseAddress;
		_scripts = scripts;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_houseAddress);
		buffer.WriteH(_scripts.Count);
		foreach (var script in _scripts)
		{
			buffer.WriteC(script.Id);
			if (script.HasData)
			{
				buffer.WriteH(8 + script.CompressedBytes.Length + ScriptPadding.Length);
				buffer.WriteD(script.CompressedBytes.Length + ScriptPadding.Length);
				buffer.WriteD(script.UncompressedSize);
				buffer.WriteB(script.CompressedBytes);
				buffer.WriteB(ScriptPadding);
			}
			else
			{
				buffer.WriteH(0);
			}
		}
	}
}
