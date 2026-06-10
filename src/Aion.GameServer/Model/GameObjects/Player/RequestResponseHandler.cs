using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.GameObjects.Player;

/// <summary>
/// Non-generic base for RequestResponseHandler&lt;T&gt;. Java has only the single generic class; C# adds this base so
/// heterogeneous handlers can be stored together and invoked through the type-independent Handle() — the C#
/// equivalent of Java's <c>RequestResponseHandler&lt;? extends Creature&gt;</c> wildcard (Handle does not use T).
/// </summary>
public abstract class RequestResponseHandler
{
    /// <summary>Called when a response is received (0 = no, 1 = yes).</summary>
    public abstract void Handle(Player responder, int response);
}

/// <summary>
/// Implemented by handlers of CM_QUESTION_RESPONSE responses.
/// Java parity: model/gameobjects/player/RequestResponseHandler&lt;T extends Creature&gt;.
/// </summary>
public abstract class RequestResponseHandler<T> : RequestResponseHandler where T : Creature
{
    private readonly T requester;

    public RequestResponseHandler(T requester)
    {
        this.requester = requester;
    }

    /// <summary>Called when a response is received (0 = no, 1 = yes).</summary>
    public override void Handle(Player responder, int response)
    {
        if (response == 0)
            DenyRequest(requester, responder);
        else
            AcceptRequest(requester, responder);
    }

    /// <summary>Called when the player accepts a request.</summary>
    public abstract void AcceptRequest(T requester, Player responder);

    /// <summary>Called when the player denies a request.</summary>
    public virtual void DenyRequest(T requester, Player responder)
    {
    }
}
