using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerRecallInstantRequestServiceTests
{
	[Fact]
	public void SendRecallRequest_RegistersQuestionAndSendsSummonWindow()
	{
		var effector = CreatePlayer(1001, "Caster");
		var effected = CreatePlayer(2001, "Target");
		var destination = new WorldPosition(210010000, 10, 20, 30, 9, InstanceId: 1);
		var service = new PlayerRecallInstantRequestService();

		var result = service.SendRecallRequest(effector, effected, destination);

		Assert.Equal(RecallInstantRequestStatus.Requested, result.Status);
		Assert.Equal(1, effected.ResponseRequester.Count);
		Assert.NotNull(effected.PendingRecallInstantRequest);
		Assert.Equal(SmQuestionWindow.SummonPartyAcceptRequest, effected.PendingRecallInstantRequest.QuestionId);
		var intent = Assert.Single(result.PacketIntents);
		Assert.Equal(effected.ObjectId, intent.RecipientObjectId);
		var question = Assert.IsType<SmQuestionWindow>(intent.Packet);
		Assert.Equal(SmQuestionWindow.SummonPartyAcceptRequest, question.Code);
	}

	[Fact]
	public void SendRecallRequest_DuplicateQuestionKeepsOriginalPendingRequest()
	{
		var effector = CreatePlayer(1001, "Caster");
		var effected = CreatePlayer(2001, "Target");
		var service = new PlayerRecallInstantRequestService();
		var first = service.SendRecallRequest(effector, effected, new WorldPosition(210010000, 10, 20, 30, 9));

		var duplicate = service.SendRecallRequest(
			CreatePlayer(1002, "Other"),
			effected,
			new WorldPosition(210010000, 90, 80, 70, 1));

		Assert.Equal(RecallInstantRequestStatus.Requested, first.Status);
		Assert.Equal(RecallInstantRequestStatus.DuplicateRequest, duplicate.Status);
		Assert.Equal(effector.ObjectId, effected.PendingRecallInstantRequest?.EffectorObjectId);
		Assert.Equal(1, effected.ResponseRequester.Count);
		Assert.Empty(duplicate.PacketIntents);
	}

	[Fact]
	public void HandleResponse_DenyClearsPendingAndNotifiesBothPlayers()
	{
		var effector = CreatePlayer(1001, "Caster");
		var effected = CreatePlayer(2001, "Target");
		var service = new PlayerRecallInstantRequestService();
		service.SendRecallRequest(effector, effected, new WorldPosition(210010000, 10, 20, 30, 9));

		var result = service.HandleResponse(
			effected,
			SmQuestionWindow.SummonPartyAcceptRequest,
			response: 0,
			id => id == effector.ObjectId ? effector : null);

		Assert.True(result.Handled);
		Assert.Equal(RecallInstantResponseStatus.Denied, result.Status);
		Assert.Null(effected.PendingRecallInstantRequest);
		Assert.Equal(0, effected.ResponseRequester.Count);
		Assert.Collection(
			result.PacketIntents,
			intent =>
			{
				Assert.Equal(effector.ObjectId, intent.RecipientObjectId);
				Assert.IsType<SmSystemMessage>(intent.Packet);
			},
			intent =>
			{
				Assert.Equal(effected.ObjectId, intent.RecipientObjectId);
				Assert.IsType<SmSystemMessage>(intent.Packet);
			});
	}

	[Fact]
	public void HandleResponse_AcceptTeleportsEffectedPlayerToCapturedDestination()
	{
		var effector = CreatePlayer(1001, "Caster");
		var effected = CreatePlayer(2001, "Target");
		var destination = new WorldPosition(210010000, 10, 20, 30, 9, InstanceId: 1);
		var service = new PlayerRecallInstantRequestService();
		service.SendRecallRequest(effector, effected, destination);

		var result = service.HandleResponse(
			effected,
			SmQuestionWindow.SummonPartyAcceptRequest,
			response: 1,
			id => id == effector.ObjectId ? effector : null);

		Assert.True(result.Handled);
		Assert.Equal(RecallInstantResponseStatus.Accepted, result.Status);
		Assert.Equal(destination, effected.Position);
		Assert.NotNull(result.Teleport);
		Assert.Equal(destination, result.Teleport.Destination);
		Assert.Null(effected.PendingRecallInstantRequest);
		Assert.Equal(0, effected.ResponseRequester.Count);
		Assert.Empty(result.PacketIntents);
	}

	[Fact]
	public void HandleResponse_EffectorMissingConsumesRequestWithoutTeleport()
	{
		var effector = CreatePlayer(1001, "Caster");
		var effected = CreatePlayer(2001, "Target");
		var originalPosition = effected.Position;
		var service = new PlayerRecallInstantRequestService();
		service.SendRecallRequest(effector, effected, new WorldPosition(210010000, 10, 20, 30, 9));

		var result = service.HandleResponse(
			effected,
			SmQuestionWindow.SummonPartyAcceptRequest,
			response: 1,
			_ => null);

		Assert.False(result.Handled);
		Assert.Equal(RecallInstantResponseStatus.EffectorMissing, result.Status);
		Assert.Equal(originalPosition, effected.Position);
		Assert.Null(effected.PendingRecallInstantRequest);
		Assert.Equal(0, effected.ResponseRequester.Count);
	}

	private static Player CreatePlayer(int objectId, string name)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = "ELYOS",
			Position = new WorldPosition(210010000, 1, 2, 3, 0, InstanceId: 1),
		};
	}
}
