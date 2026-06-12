namespace Aion.GameServer.Model.GameObjects;

public sealed class PlayerGroupMember
{
	public PlayerGroupMember(Player player)
	{
		// Java parity: model/team/group/PlayerGroupMember extends PlayerTeamMember and stores the wrapped Player.
		Player = player;
	}

	public Player Player { get; }

	public int ObjectId => Player.ObjectId;

	public string Name => Player.Name;

	public long LastOnlineTimeMillis { get; private set; }

	public bool IsOnline => Player.IsOnline;

	public float X => Player.GetPosition().X;

	public float Y => Player.GetPosition().Y;

	public float Z => Player.GetPosition().Z;

	public byte Heading => Player.GetPosition().Heading;

	public int Level => Player.Level;

	public void UpdateLastOnlineTime(DateTimeOffset now)
	{
		// Java parity: model/team/PlayerTeamMember.updateLastOnlineTime uses System.currentTimeMillis().
		LastOnlineTimeMillis = now.ToUnixTimeMilliseconds();
	}
}
