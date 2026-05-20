namespace Aion.ChatServer.Services;

public enum GsAuthResponse
{
	Authed = 0,
	NotAuthed = 1,
	AlreadyRegistered = 2,
}

public interface IGameServerService
{
	byte? GameServerId { get; }

	bool IsOnline { get; }

	GsAuthResponse RegisterGameServer(byte gameServerId, string password);

	void SetOffline();
}
