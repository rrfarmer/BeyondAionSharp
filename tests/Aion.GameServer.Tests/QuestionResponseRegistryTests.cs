using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class QuestionResponseRegistryTests
{
	[Fact]
	public void PutRequest_RejectsNullAndDuplicateQuestionIdLikeJavaPutIfAbsent()
	{
		var registry = new QuestionResponseRegistry();
		var request = new QuestionResponseRequest(1001, QuestionResponseRequestKind.LeagueInvite, Payload: "invite");

		Assert.False(registry.PutRequest(SmQuestionWindow.UnionInviteMe, null));
		Assert.True(registry.PutRequest(SmQuestionWindow.UnionInviteMe, request));
		Assert.False(registry.PutRequest(
			SmQuestionWindow.UnionInviteMe,
			new QuestionResponseRequest(1002, QuestionResponseRequestKind.FriendInvite)));
		Assert.Equal(1, registry.Count);
	}

	[Fact]
	public void Respond_RemovesBeforeDispatchAndMapsZeroToDenyNonzeroToAcceptLikeJavaHandle()
	{
		var registry = new QuestionResponseRegistry();
		var request = new QuestionResponseRequest(1001, QuestionResponseRequestKind.LeagueInvite);
		Assert.True(registry.PutRequest(SmQuestionWindow.UnionInviteMe, request));

		var deny = Assert.IsType<QuestionResponseDispatch>(registry.Respond(SmQuestionWindow.UnionInviteMe, responseCode: 0));

		Assert.Equal(SmQuestionWindow.UnionInviteMe, deny.QuestionId);
		Assert.Equal(0, deny.ResponseCode);
		Assert.False(deny.Accepted);
		Assert.Same(request, deny.Request);
		Assert.Equal(0, registry.Count);
		Assert.Null(registry.Respond(SmQuestionWindow.UnionInviteMe, responseCode: 1));

		Assert.True(registry.PutRequest(SmQuestionWindow.UnionInviteMe, request));
		var accept = Assert.IsType<QuestionResponseDispatch>(registry.Respond(SmQuestionWindow.UnionInviteMe, responseCode: 7));
		Assert.True(accept.Accepted);
		Assert.Equal(7, accept.ResponseCode);
		Assert.Equal(0, registry.Count);
	}

	[Fact]
	public void Remove_DropsRegisteredQuestionLikeJavaRemove()
	{
		var registry = new QuestionResponseRegistry();
		Assert.True(registry.PutRequest(
			SmQuestionWindow.UnionInviteMe,
			new QuestionResponseRequest(1001, QuestionResponseRequestKind.LeagueInvite)));

		Assert.True(registry.Remove(SmQuestionWindow.UnionInviteMe));
		Assert.False(registry.Remove(SmQuestionWindow.UnionInviteMe));
		Assert.Null(registry.Respond(SmQuestionWindow.UnionInviteMe, responseCode: 1));
	}

	[Fact]
	public void DenyAll_ReturnsDenyDispatchesAndClearsLikeJavaDenyAll()
	{
		var registry = new QuestionResponseRegistry();
		Assert.True(registry.PutRequest(
			SmQuestionWindow.UnionInviteMe,
			new QuestionResponseRequest(1001, QuestionResponseRequestKind.LeagueInvite)));
		Assert.True(registry.PutRequest(
			SmQuestionWindow.BuddyListAddBuddyRequest,
			new QuestionResponseRequest(1002, QuestionResponseRequestKind.FriendInvite)));

		var dispatches = registry.DenyAll();

		Assert.Equal(2, dispatches.Count);
		Assert.All(dispatches, dispatch =>
		{
			Assert.Equal(0, dispatch.ResponseCode);
			Assert.False(dispatch.Accepted);
		});
		Assert.Equal(
			new[] { SmQuestionWindow.UnionInviteMe, SmQuestionWindow.BuddyListAddBuddyRequest }.Order().ToArray(),
			dispatches.Select(dispatch => dispatch.QuestionId).Order().ToArray());
		Assert.Equal(0, registry.Count);
		Assert.Empty(registry.DenyAll());
	}
}
