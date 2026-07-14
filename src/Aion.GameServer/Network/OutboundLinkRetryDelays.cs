namespace Aion.GameServer.Network;

/// <summary>
/// Retry intervals used by the outbound LoginServer and ChatServer bridges.
/// The defaults mirror the Java 4.8 connectors; tests inject shorter intervals.
/// </summary>
internal sealed record OutboundLinkRetryDelays(
	TimeSpan SocketFailure,
	TimeSpan IoFailure,
	TimeSpan AuthedReconnect,
	TimeSpan PreAuthReconnect)
{
	public static OutboundLinkRetryDelays JavaDefaults { get; } = new(
		TimeSpan.FromSeconds(10),
		TimeSpan.FromSeconds(60),
		TimeSpan.FromSeconds(5),
		TimeSpan.FromSeconds(15));
}
