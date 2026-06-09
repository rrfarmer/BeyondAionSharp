using Aion.Commons.Network;
using Aion.GameServer.Data;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class PlayerEnterWorldRepositoryPassportRestoreTests
{
	[Fact]
	public void RestoreAccountPassportState_HydratesPlayerSnapshotUsedByAtreianPassportPacket()
	{
		var player = new Player
		{
			ObjectId = 1001,
			AccountId = 77,
			CreationDate = new DateTime(2020, 5, 6, 7, 8, 9, DateTimeKind.Utc),
		};
		var lastStamp = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
		var snapshot = new AccountPassportRestoreSnapshot(
			[
				new Passport(
					id: 3003,
					rewarded: true,
					arriveDate: DateTimeOffset.FromUnixTimeSeconds(1_717_286_400).UtcDateTime)
			],
			Stamps: 9,
			LastStamp: lastStamp);

		MySqlPlayerEnterWorldRepository.RestoreAccountPassportState(player, snapshot);

		Assert.Same(snapshot.Passports, player.Passports);
		Assert.Equal(9, player.PassportStamps);
		Assert.Equal(lastStamp, player.LastPassportStamp);

		var payload = SerializeUnencryptedPayload(new SmAtreianPassport(
			player.Passports,
			player.PassportStamps,
			player.CreationDate));
		Assert.Equal(2020, ReadShort(payload, 0));
		Assert.Equal(5, ReadShort(payload, 2));
		Assert.Equal(6, ReadShort(payload, 4));
		Assert.Equal(1, ReadShort(payload, 6));
		Assert.Equal(3003, ReadInt(payload, 8));
		Assert.Equal(9, ReadInt(payload, 12));
		Assert.Equal(2, ReadInt(payload, 16)); // Passport.GetRewardStatus().TAKEN.
		Assert.Equal(1_717_286_400, ReadInt(payload, 20));
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static int ReadInt(byte[] payload, int offset)
	{
		return BitConverter.ToInt32(payload, offset);
	}

	private static int ReadShort(byte[] payload, int offset)
	{
		return BitConverter.ToUInt16(payload, offset);
	}
}
