using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class RiftPortalDialogServiceTests
{
	[Fact]
	public void CreateDialogRequest_ForOrdinaryRift_ReturnsDirectPortalQuestion()
	{
		var service = new RiftPortalDialogService();
		var portal = CreatePortal(new RiftDefinition(2120, "ELTNEN", "ELTNEN_AM", "MORHEIM_AS", 36, 20, 40, "ASMODIANS"));
		var player = CreatePlayer("ELYOS");

		var result = service.CreateDialogRequest(player, portal);

		Assert.True(result.Requested);
		Assert.Equal(RiftPortalDialogStatus.Requested, result.Status);
		var payload = ReadQuestionPayload(Assert.IsType<SmQuestionWindow>(result.QuestionWindow));
		Assert.Equal(SmQuestionWindow.DirectPortalPassConfirm, payload.Code);
		Assert.Equal(0, payload.SenderObjectId);
		Assert.Equal(0, payload.RangeOrCooldownSeconds);
		Assert.False(payload.HasRangeOrCooldown);
	}

	[Fact]
	public void CreateDialogRequest_ForVortexRift_ReturnsVortexQuestionWithOwnerRange()
	{
		var service = new RiftPortalDialogService();
		var portal = CreatePortal(
			new RiftDefinition(1170, "KAISINEL", "KAISINEL_AM", "KAISINEL_AS", 24, 45, 65, "ASMODIANS", IsVortex: true));
		var player = CreatePlayer("ELYOS");

		var result = service.CreateDialogRequest(player, portal);

		Assert.True(result.Requested);
		var payload = ReadQuestionPayload(Assert.IsType<SmQuestionWindow>(result.QuestionWindow));
		Assert.Equal(SmQuestionWindow.VortexPortalPassConfirm, payload.Code);
		Assert.Equal(portal.MasterNpc.ObjectId, payload.SenderObjectId);
		Assert.Equal(5, payload.RangeOrCooldownSeconds);
		Assert.True(payload.HasRangeOrCooldown);
	}

	[Theory]
	[InlineData("ELYOS", true)]
	[InlineData("ASMODIANS", false)]
	public void CreateDialogRequest_ForInvasionRift_RequiresOppositeRaceDestination(string playerRace, bool expectedRequested)
	{
		var service = new RiftPortalDialogService();
		var portal = CreatePortal(
			new RiftDefinition(
				2189,
				"CYGNEA_VIL1M",
				"CYGNEA_VIL1M",
				"ENSHAR_VIL1S",
				72,
				55,
				65,
				"ASMODIANS",
				IsInvasionRift: true));
		var player = CreatePlayer(playerRace);

		var result = service.CreateDialogRequest(player, portal);

		Assert.Equal(expectedRequested, result.Requested);
		if (expectedRequested)
		{
			Assert.Equal(RiftPortalDialogStatus.Requested, result.Status);
			Assert.NotNull(result.QuestionWindow);
		}
		else
		{
			Assert.Equal(RiftPortalDialogStatus.InvasionRaceMismatch, result.Status);
			Assert.Null(result.QuestionWindow);
		}
	}

	private static Player CreatePlayer(string race)
	{
		return new Player
		{
			ObjectId = 100,
			Race = race,
			Position = new WorldPosition(210070000, 0, 0, 0, 0),
		};
	}

	private static RiftPortalState CreatePortal(RiftDefinition definition)
	{
		var template = new NpcTemplateSummary(730100, "Rift", 0, 1, "NORMAL", "NORMAL", "NONE", "NONE", "NPC");
		var master = new WorldNpc(
			ObjectId: 7101,
			TemplateId: 730100,
			Template: template,
			Position: new WorldPosition(210070000, 1, 2, 3, 0),
			Anchor: definition.MasterAnchor);
		var slave = new WorldNpc(
			ObjectId: 7102,
			TemplateId: 730101,
			Template: template,
			Position: new WorldPosition(220080000, 4, 5, 6, 0),
			Anchor: definition.SlaveAnchor);

		return new RiftPortalState(definition, master, slave, guardsRequested: false, despawnTimeUnixSeconds: 5000);
	}

	private static QuestionPayload ReadQuestionPayload(GameServerPacket packet)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		var payload = new QuestionPayload(
			reader.ReadD(),
			reader.ReadS(),
			reader.ReadS(),
			reader.ReadS(),
			reader.ReadD(),
			reader.ReadC() == 1,
			reader.ReadD(),
			reader.ReadD());
		Assert.Equal(0, reader.Remaining);
		return payload;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed record QuestionPayload(
		int Code,
		string FirstParameter,
		string SecondParameter,
		string ThirdParameter,
		int Unknown,
		bool HasRangeOrCooldown,
		int SenderObjectId,
		int RangeOrCooldownSeconds);
}
