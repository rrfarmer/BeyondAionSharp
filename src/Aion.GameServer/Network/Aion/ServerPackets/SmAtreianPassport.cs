using Aion.Commons.Network;
using Aion.GameServer.Model.Account;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmAtreianPassport : GameServerPacket
{
	public const int PacketOpCode = 299;
	private readonly DateOnly _creationDate;
	private readonly IReadOnlyList<Passport> _passports;
	private readonly int _stamps;

	public SmAtreianPassport(
		IReadOnlyList<Passport> passports,
		int stamps,
		DateTime creationDate)
		: base(PacketOpCode)
	{
		_passports = passports;
		_stamps = stamps;
		_creationDate = DateOnly.FromDateTime(creationDate);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_ATREIAN_PASSPORT.writeImpl.
		buffer.WriteH(_creationDate.Year);
		buffer.WriteH(_creationDate.Month);
		buffer.WriteH(_creationDate.Day);
		buffer.WriteH(_passports.Count);
		foreach (var passport in _passports)
		{
			buffer.WriteD(passport.GetId());
			buffer.WriteD(_stamps);
			buffer.WriteD(passport.GetRewardStatus().GetId());
			buffer.WriteD((int)(new DateTimeOffset(passport.GetArriveDate()).ToUnixTimeMilliseconds() / 1000));
		}
	}
}
