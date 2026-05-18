namespace Aion.LoginServer.Network.GameServer;

public enum GsAuthResponse : byte
{
	AUTHED = 0,
	NOT_AUTHED = 1,
	ALREADY_REGISTERED = 2
}
