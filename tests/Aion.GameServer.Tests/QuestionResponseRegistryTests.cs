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

		Assert.False(registry.PutRequest(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, null));
		Assert.True(registry.PutRequest(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, request));
		Assert.False(registry.PutRequest(
			SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME,
			new QuestionResponseRequest(1002, QuestionResponseRequestKind.FriendInvite)));
		Assert.Equal(1, registry.Count);
	}

	[Fact]
	public void IsRequestSlotAvailable_ReflectsJavaPutIfAbsentMessageIdGuard()
	{
		var registry = new QuestionResponseRegistry();
		var request = new QuestionResponseRequest(1001, QuestionResponseRequestKind.LeagueInvite);

		Assert.True(registry.IsRequestSlotAvailable(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME));
		Assert.False(registry.ContainsRequest(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME));

		Assert.True(registry.PutRequest(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, request));

		Assert.True(registry.ContainsRequest(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME));
		Assert.False(registry.IsRequestSlotAvailable(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME));
		Assert.True(registry.IsRequestSlotAvailable(SM_QUESTION_WINDOW.STR_BUDDYLIST_ADD_BUDDY_REQUEST));
	}

	[Fact]
	public void Respond_RemovesBeforeDispatchAndMapsZeroToDenyNonzeroToAcceptLikeJavaHandle()
	{
		var registry = new QuestionResponseRegistry();
		var request = new QuestionResponseRequest(1001, QuestionResponseRequestKind.LeagueInvite);
		Assert.True(registry.PutRequest(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, request));

		var deny = Assert.IsType<QuestionResponseDispatch>(registry.Respond(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, responseCode: 0));

		Assert.Equal(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, deny.QuestionId);
		Assert.Equal(0, deny.ResponseCode);
		Assert.False(deny.Accepted);
		Assert.Same(request, deny.Request);
		Assert.Equal(0, registry.Count);
		Assert.Null(registry.Respond(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, responseCode: 1));

		Assert.True(registry.PutRequest(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, request));
		var accept = Assert.IsType<QuestionResponseDispatch>(registry.Respond(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, responseCode: 7));
		Assert.True(accept.Accepted);
		Assert.Equal(7, accept.ResponseCode);
		Assert.Equal(0, registry.Count);
	}

	[Fact]
	public void Remove_DropsRegisteredQuestionLikeJavaRemove()
	{
		var registry = new QuestionResponseRegistry();
		Assert.True(registry.PutRequest(
			SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME,
			new QuestionResponseRequest(1001, QuestionResponseRequestKind.LeagueInvite)));

		Assert.True(registry.Remove(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME));
		Assert.False(registry.Remove(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME));
		Assert.Null(registry.Respond(SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, responseCode: 1));
	}

	[Fact]
	public void DenyAll_ReturnsDenyDispatchesAndClearsLikeJavaDenyAll()
	{
		var registry = new QuestionResponseRegistry();
		Assert.True(registry.PutRequest(
			SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME,
			new QuestionResponseRequest(1001, QuestionResponseRequestKind.LeagueInvite)));
		Assert.True(registry.PutRequest(
			SM_QUESTION_WINDOW.STR_BUDDYLIST_ADD_BUDDY_REQUEST,
			new QuestionResponseRequest(1002, QuestionResponseRequestKind.FriendInvite)));

		var dispatches = registry.DenyAll();

		Assert.Equal(2, dispatches.Count);
		Assert.All(dispatches, dispatch =>
		{
			Assert.Equal(0, dispatch.ResponseCode);
			Assert.False(dispatch.Accepted);
		});
		Assert.Equal(
			new[] { SM_QUESTION_WINDOW.STR_MSGBOX_UNION_INVITE_ME, SM_QUESTION_WINDOW.STR_BUDDYLIST_ADD_BUDDY_REQUEST }.Order().ToArray(),
			dispatches.Select(dispatch => dispatch.QuestionId).Order().ToArray());
		Assert.Equal(0, registry.Count);
		Assert.Empty(registry.DenyAll());
	}
}
