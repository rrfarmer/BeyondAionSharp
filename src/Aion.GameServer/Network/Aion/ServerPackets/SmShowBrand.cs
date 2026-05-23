using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmShowBrand : GameServerPacket
{
	public const int PacketOpCode = 249;
	private readonly IReadOnlyDictionary<int, int> _targetObjectIdsByBrandId;

	public SmShowBrand(int brandId, int targetObjectId)
		: this(new Dictionary<int, int> { [brandId] = targetObjectId }, resetWhenEmpty: false)
	{
		// Java parity: network/aion/serverpackets/SM_SHOW_BRAND(int iconId, int targetObjectId).
	}

	public SmShowBrand(IReadOnlyDictionary<int, int> targetObjectIdsByBrandId)
		: this(targetObjectIdsByBrandId, resetWhenEmpty: true)
	{
		// Java parity: network/aion/serverpackets/SM_SHOW_BRAND(Map<Integer, Integer> targetIdsByIconId).
	}

	private SmShowBrand(IReadOnlyDictionary<int, int> targetObjectIdsByBrandId, bool resetWhenEmpty)
		: base(PacketOpCode)
	{
		_targetObjectIdsByBrandId = targetObjectIdsByBrandId.Count == 0 && resetWhenEmpty
			? Enumerable.Range(0, 16).ToDictionary(brandId => brandId, _ => 0)
			: new Dictionary<int, int>(targetObjectIdsByBrandId);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_SHOW_BRAND.writeImpl.
		buffer.WriteH(_targetObjectIdsByBrandId.Count);
		foreach (var (brandId, targetObjectId) in _targetObjectIdsByBrandId)
		{
			buffer.WriteD(1);
			buffer.WriteD(brandId);
			buffer.WriteD(targetObjectId);
		}
	}
}
