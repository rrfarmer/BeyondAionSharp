using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerAllianceMemberInfoTests
{
	[Fact]
	public void PlayerAllianceEvent_JavaIdsMatchLegacyEnum()
	{
		Assert.Equal(0, (int)PlayerAllianceEvent.Leave);
		Assert.Equal(0, (int)PlayerAllianceEvent.Banned);
		Assert.Equal(1, (int)PlayerAllianceEvent.Movement);
		Assert.Equal(3, (int)PlayerAllianceEvent.Disconnected);
		Assert.Equal(5, (int)PlayerAllianceEvent.Join);
		Assert.Equal(5, (int)PlayerAllianceEvent.MemberGroupChange);
		Assert.Equal(7, (int)PlayerAllianceEvent.EnterOffline);
		Assert.Equal(65, (int)PlayerAllianceEvent.UpdateEffects);
		Assert.Equal(13, (int)PlayerAllianceEvent.Reconnect);
		Assert.Equal(13, (int)PlayerAllianceEvent.Enter);
		Assert.Equal(13, (int)PlayerAllianceEvent.Update);
		Assert.Equal(13, (int)PlayerAllianceEvent.AppointCaptain);
	}

	[Fact]
	public void CreateMovementUpdatePlan_ReturnsAllExceptPlayerIntentsLikeJavaPlayerAllianceUpdateEvent()
	{
		var planner = new PlayerAllianceMovementUpdatePlanner();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var subject = new Player
		{
			ObjectId = 1002,
			Name = "Subject",
			IsOnline = true,
			PlayerClass = "CLERIC",
			Gender = "FEMALE",
			Level = 45,
			Position = new WorldPosition(220010000, 11, 22, 33, 64),
		};
		var other = new Player { ObjectId = 1003, Name = "Other", IsOnline = true };

		var plan = Assert.IsType<PlayerAllianceMemberInfoUpdatePlan>(
			planner.CreateMovementUpdatePlan(88001, [leader, subject, other], subject));

		Assert.Equal(88001, plan.AllianceId);
		Assert.Equal(1002, plan.SubjectObjectId);
		Assert.Equal(PlayerAllianceEvent.Movement, plan.Event);
		Assert.Equal(0, plan.Slot);
		Assert.Collection(
			plan.MemberInfoIntents,
			intent => AssertMovementIntent(intent, recipientObjectId: 1001, subjectObjectId: 1002),
			intent => AssertMovementIntent(intent, recipientObjectId: 1003, subjectObjectId: 1002));
		Assert.DoesNotContain(plan.MemberInfoIntents, intent => intent.RecipientObjectId == subject.ObjectId);
	}

	[Fact]
	public void CreateMovementUpdatePlan_ReturnsNullForMissingAllianceMember()
	{
		var planner = new PlayerAllianceMovementUpdatePlanner();
		var member = new Player { ObjectId = 1001, IsOnline = true };
		var outsider = new Player { ObjectId = 1002, IsOnline = true };

		var plan = planner.CreateMovementUpdatePlan(88001, [member], outsider);

		Assert.Null(plan);
	}

	private static void AssertMovementIntent(
		PlayerAllianceMemberInfoIntent intent,
		int recipientObjectId,
		int subjectObjectId)
	{
		Assert.Equal(recipientObjectId, intent.RecipientObjectId);
		Assert.Equal(subjectObjectId, intent.SubjectObjectId);
		Assert.Equal(PlayerAllianceEvent.Movement, intent.Event);
		var plan = Assert.IsType<PlayerAllianceMemberInfoPacketPlan>(intent.PacketPlan);
		Assert.Equal(88001, plan.AllianceId);
		Assert.Equal(subjectObjectId, plan.MemberObjectId);
		Assert.Equal(PlayerAllianceEvent.Movement, plan.RequestedEvent);
		Assert.Equal(PlayerAllianceEvent.Movement, plan.EffectiveEvent);
		Assert.Equal(0, plan.Slot);
		Assert.True(plan.IsOnline);
		Assert.Equal(10, plan.PrefixSnapshot.ClassId);
		Assert.Equal(1, plan.PrefixSnapshot.GenderId);
		Assert.Equal(45, plan.PrefixSnapshot.Level);
		Assert.Equal((int)PlayerAllianceEvent.Movement, plan.PrefixSnapshot.EventId);
		Assert.Equal(1, plan.PrefixSnapshot.AlwaysOne);
		Assert.Equal(0, plan.PrefixSnapshot.AllianceUnknown);
		AssertAllianceMemberInfoMovementPayload(intent.CreatePacket());
	}

	private static void AssertAllianceMemberInfoMovementPayload(GameServerPacket? packet)
	{
		var actual = Assert.IsType<SmAllianceMemberInfo>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(actual));
		for (var i = 0; i < 11; i++)
			reader.ReadD();
		reader.ReadF();
		reader.ReadF();
		reader.ReadF();
		Assert.Equal(10, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(45, (int)reader.ReadC());
		Assert.Equal((int)PlayerAllianceEvent.Movement, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
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
