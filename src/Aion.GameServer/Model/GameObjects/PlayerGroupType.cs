namespace Aion.GameServer.Model.GameObjects;

public enum PlayerGroupType
{
	// Java parity: model/team/TeamType.GROUP.
	Group,

	// Java parity: model/team/TeamType.AUTO_GROUP.
	AutoGroup,
}

public static class PlayerGroupTypeExtensions
{
	public static (int Type, int SubType) ToJavaPacketFields(this PlayerGroupType teamType)
	{
		// Java parity: model/team/TeamType.getType/getSubType for GROUP and AUTO_GROUP.
		return teamType switch
		{
			PlayerGroupType.Group => (0x3F, 0),
			PlayerGroupType.AutoGroup => (0x02, 1),
			_ => (0x3F, 0),
		};
	}
}
