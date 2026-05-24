using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerLeagueInvitePlannerTests
{
	[Fact]
	public void CreateDenyPlan_SendsRequesterRejectMessageLikeJavaLeagueInviteEvent()
	{
		var planner = new PlayerLeagueInvitePlanner();

		var plan = planner.CreateDenyPlan(requesterObjectId: 1001, responderName: "Responder");

		Assert.Equal(1001, plan.RequesterObjectId);
		Assert.Equal("Responder", plan.ResponderName);
		Assert.Equal(1001, plan.SystemMessageIntent.RecipientObjectId);
		Assert.Equal(1300190, plan.SystemMessageIntent.Message.MessageId);
		AssertSystemMessagePayload(plan.SystemMessageIntent.Message, 1300190, "Responder");
	}

	[Fact]
	public void CreateDenyPlan_RejectsInvalidRequesterOrResponder()
	{
		var planner = new PlayerLeagueInvitePlanner();

		Assert.Throws<ArgumentOutOfRangeException>(() => planner.CreateDenyPlan(0, "Responder"));
		Assert.Throws<ArgumentException>(() => planner.CreateDenyPlan(1001, ""));
	}

	private static void AssertSystemMessagePayload(
		SmSystemMessage packet,
		int expectedMessageId,
		params string[] expectedParameters)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedMessageId, reader.ReadD());
		Assert.Equal(expectedParameters.Length, (int)reader.ReadC());
		foreach (var expectedParameter in expectedParameters)
			Assert.Equal(expectedParameter, reader.ReadS());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
