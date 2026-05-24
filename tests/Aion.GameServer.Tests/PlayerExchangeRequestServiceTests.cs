using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerExchangeRequestServiceTests
{
	[Fact]
	public void ClientPacketFactory_ParsesExchangeRequestPacket()
	{
		var packet = Assert.IsType<CmExchangeRequest>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(63, buffer =>
			{
				buffer.WriteD(2001);
			}), GameConnectionState.InGame));

		Assert.Equal(2001, packet.TargetObjectId);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(63, buffer =>
		{
			buffer.WriteD(2001);
		}), GameConnectionState.Authed));
	}

	[Fact]
	public void SendExchangeRequest_RegistersQuestionAndSendsRequesterAndTargetPackets()
	{
		var requester = CreatePlayer(1001, "Requester");
		var target = CreatePlayer(2001, "Target");
		var service = new PlayerExchangeRequestService();

		var plan = service.SendExchangeRequest(requester, target);

		Assert.Equal(ExchangeRequestStatus.Requested, plan.Status);
		Assert.Equal(1, target.ResponseRequester.Count);
		Assert.NotNull(target.PendingExchangeRequest);
		Assert.Equal(SmQuestionWindow.ExchangeAcceptRequest, target.PendingExchangeRequest.QuestionId);
		Assert.Collection(
			plan.PacketIntents,
			intent =>
			{
				Assert.Equal(requester.ObjectId, intent.RecipientObjectId);
				Assert.IsType<SmSystemMessage>(intent.Packet);
			},
			intent =>
			{
				Assert.Equal(target.ObjectId, intent.RecipientObjectId);
				var question = Assert.IsType<SmQuestionWindow>(intent.Packet);
				Assert.Equal(SmQuestionWindow.ExchangeAcceptRequest, question.Code);
			});
	}

	[Fact]
	public void SendExchangeRequest_DuplicateQuestionReportsBusyAndKeepsOriginalPendingRequest()
	{
		var requester = CreatePlayer(1001, "Requester");
		var target = CreatePlayer(2001, "Target");
		var service = new PlayerExchangeRequestService();
		var first = service.SendExchangeRequest(requester, target);

		var duplicate = service.SendExchangeRequest(CreatePlayer(1002, "Other"), target);

		Assert.Equal(ExchangeRequestStatus.Requested, first.Status);
		Assert.Equal(ExchangeRequestStatus.TargetBusy, duplicate.Status);
		Assert.Equal(1001, target.PendingExchangeRequest?.RequesterObjectId);
		Assert.Equal(1, target.ResponseRequester.Count);
		Assert.Single(duplicate.PacketIntents);
		Assert.IsType<SmSystemMessage>(duplicate.PacketIntents[0].Packet);
	}

	[Fact]
	public void SendExchangeRequest_TargetDenyTradeUsesJavaDeniedStatusBit()
	{
		var requester = CreatePlayer(1001, "Requester");
		var target = CreatePlayer(2001, "Target");
		target.Settings = new PlayerSettings { Deny = PlayerSettings.DenyTradeRequests };
		var service = new PlayerExchangeRequestService();

		var plan = service.SendExchangeRequest(requester, target);

		Assert.Equal(ExchangeRequestStatus.TargetDeniedTrade, plan.Status);
		Assert.Single(plan.PacketIntents);
		Assert.Equal(0, target.ResponseRequester.Count);
		Assert.Null(target.PendingExchangeRequest);
	}

	[Fact]
	public void SendExchangeRequest_RejectsFarOrHiddenTargetsBeforeQuestionRegistration()
	{
		var requester = CreatePlayer(1001, "Requester");
		var farTarget = CreatePlayer(2001, "Target", x: 20);
		var hiddenTarget = CreatePlayer(2002, "Hidden");
		hiddenTarget.VisualState = PlayerVisualStates.Hide1;
		var service = new PlayerExchangeRequestService();

		var far = service.SendExchangeRequest(requester, farTarget);
		var hidden = service.SendExchangeRequest(requester, hiddenTarget);

		Assert.Equal(ExchangeRequestStatus.TooFar, far.Status);
		Assert.Equal(ExchangeRequestStatus.TargetInvisible, hidden.Status);
		Assert.Equal(0, farTarget.ResponseRequester.Count);
		Assert.Equal(0, hiddenTarget.ResponseRequester.Count);
	}

	[Fact]
	public void HandleResponse_DenyClearsPendingAndNotifiesRequester()
	{
		var requester = CreatePlayer(1001, "Requester");
		var target = CreatePlayer(2001, "Target");
		var service = new PlayerExchangeRequestService();
		service.SendExchangeRequest(requester, target);

		var result = service.HandleResponse(
			target,
			SmQuestionWindow.ExchangeAcceptRequest,
			response: 0,
			id => id == requester.ObjectId ? requester : null);

		Assert.True(result.Handled);
		Assert.Equal(ExchangeResponseStatus.Denied, result.Status);
		Assert.False(requester.IsTrading);
		Assert.False(target.IsTrading);
		Assert.Null(target.PendingExchangeRequest);
		Assert.Equal(0, target.ResponseRequester.Count);
		var intent = Assert.Single(result.PacketIntents);
		Assert.Equal(requester.ObjectId, intent.RecipientObjectId);
		Assert.IsType<SmSystemMessage>(intent.Packet);
	}

	[Fact]
	public void HandleResponse_AcceptStartsRepresentedExchangeForBothPlayers()
	{
		var requester = CreatePlayer(1001, "Requester");
		var target = CreatePlayer(2001, "Target");
		var service = new PlayerExchangeRequestService();
		service.SendExchangeRequest(requester, target);

		var result = service.HandleResponse(
			target,
			SmQuestionWindow.ExchangeAcceptRequest,
			response: 1,
			id => id == requester.ObjectId ? requester : null);

		Assert.True(result.Handled);
		Assert.Equal(ExchangeResponseStatus.Accepted, result.Status);
		Assert.True(requester.IsTrading);
		Assert.True(target.IsTrading);
		Assert.Null(target.PendingExchangeRequest);
		Assert.Equal(0, target.ResponseRequester.Count);
		Assert.Collection(
			result.PacketIntents,
			intent =>
			{
				Assert.Equal(target.ObjectId, intent.RecipientObjectId);
				Assert.IsType<SmExchangeRequest>(intent.Packet);
			},
			intent =>
			{
				Assert.Equal(requester.ObjectId, intent.RecipientObjectId);
				Assert.IsType<SmExchangeRequest>(intent.Packet);
			});
	}

	private static Player CreatePlayer(int objectId, string name, float x = 0)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = "ELYOS",
			Position = new WorldPosition(210010000, x, 0, 0, 0),
		};
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
	}
}
