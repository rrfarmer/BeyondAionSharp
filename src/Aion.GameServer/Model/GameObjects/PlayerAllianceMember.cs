namespace Aion.GameServer.Model.GameObjects;

public sealed class PlayerAllianceMember
{
	public PlayerAllianceMember(Player player, int allianceId, int allianceGroupId)
	{
		// Java parity: model/team/alliance/PlayerAllianceMember wraps Player and stores alliance id through PlayerAllianceGroup.addMember.
		Player = player;
		AllianceId = allianceId;
		AllianceGroupId = allianceGroupId;
	}

	public Player Player { get; }

	public int ObjectId => Player.ObjectId;

	public string Name => Player.Name;

	public int AllianceId { get; private set; }

	public int AllianceGroupId { get; private set; }

	public long LastOnlineTimeMillis { get; private set; }

	public bool IsOnline => Player.IsOnline;

	public void MoveToAllianceGroup(int allianceGroupId)
	{
		// Java parity: model/team/alliance/PlayerAllianceMember.setPlayerAllianceGroup changes the member's PlayerAllianceGroup.
		AllianceGroupId = allianceGroupId;
	}

	public void ClearAllianceGroup()
	{
		// Java parity: model/team/alliance/PlayerAllianceGroup.onRemoveMember clears PlayerAllianceMember.playerAllianceGroup.
		AllianceGroupId = 0;
		AllianceId = 0;
	}

	public void UpdateLastOnlineTime(DateTimeOffset now)
	{
		// Java parity: model/team/PlayerTeamMember.updateLastOnlineTime uses System.currentTimeMillis().
		LastOnlineTimeMillis = now.ToUnixTimeMilliseconds();
	}
}
