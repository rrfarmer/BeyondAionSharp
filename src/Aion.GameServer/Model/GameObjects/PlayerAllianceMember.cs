namespace Aion.GameServer.Model.Team.Alliance;

// Java parity: model/team/alliance/PlayerAllianceMember extends PlayerTeamMember (-> ITeamMember<Player>).
public class PlayerAllianceMember : PlayerTeamMember
{
	public PlayerAllianceMember(Player player, int allianceId, int allianceGroupId) : base(player)
	{
		// Java parity: model/team/alliance/PlayerAllianceMember wraps Player and stores alliance id through PlayerAllianceGroup.addMember.
		Player = player;
		AllianceId = allianceId;
		AllianceGroupId = allianceGroupId;
	}

	// Java parity: model/team/alliance/PlayerAllianceMember(Player) — allianceId set later via PlayerAllianceGroup.addMember.
	public PlayerAllianceMember(Player player) : this(player, 0, 0)
	{
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

	public void ClearAllianceGroupReference()
	{
		// Java parity: model/team/alliance/PlayerAllianceGroup.onRemoveMember clears only the PlayerAllianceGroup reference.
		AllianceGroupId = 0;
	}

	public void UpdateLastOnlineTime(DateTimeOffset now)
	{
		// Java parity: model/team/PlayerTeamMember.updateLastOnlineTime uses System.currentTimeMillis().
		LastOnlineTimeMillis = now.ToUnixTimeMilliseconds();
	}

	// Java parity: model/team/alliance/PlayerAllianceMember.getAllianceId/setAllianceId.
	public int GetAllianceId()
	{
		return AllianceId;
	}

	public void SetAllianceId(int allianceId)
	{
		AllianceId = allianceId;
	}

	// Java parity: model/team/alliance/PlayerAllianceMember.getPlayerAllianceGroup/setPlayerAllianceGroup delegate to getObject() (the Player).
	public PlayerAllianceGroup GetPlayerAllianceGroup()
	{
		return Player.GetPlayerAllianceGroup();
	}

	public void SetPlayerAllianceGroup(PlayerAllianceGroup playerAllianceGroup)
	{
		Player.SetPlayerAllianceGroup(playerAllianceGroup);
	}
}
