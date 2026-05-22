using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmStatUpdateExp : GameServerPacket
{
	public const int PacketOpCode = 8;

	private readonly long _currentExp;
	private readonly long _recoverableExp;
	private readonly long _maxExp;
	private readonly long _currentRepose;
	private readonly long _maxRepose;

	public SmStatUpdateExp(Player player, PlayerExperienceTable experienceTable)
		: base(PacketOpCode)
	{
		// Java parity: PlayerCommonData.setExp emits SM_STATUPDATE_EXP(getExpShown, recoverable, need, repose, maxRepose).
		var level = Math.Max(1, experienceTable.GetLevelForExp(player.Exp));
		var startExp = experienceTable.GetStartExpForLevel(level);
		var expNeed = GetExpNeed(experienceTable, level);
		_currentExp = Math.Max(0, player.Exp - startExp);
		_recoverableExp = player.RecoverableExp;
		_maxExp = expNeed;
		_currentRepose = player.ReposeEnergy;
		_maxRepose = level >= 10 ? (long)(expNeed * 0.25f) : 0;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_STATUPDATE_EXP.writeImpl.
		buffer.WriteQ(_currentExp);
		buffer.WriteQ(_recoverableExp);
		buffer.WriteQ(_maxExp);
		buffer.WriteQ(_currentRepose);
		buffer.WriteQ(_maxRepose);
	}

	private static long GetExpNeed(PlayerExperienceTable experienceTable, int level)
	{
		if (level <= 0 || level >= experienceTable.MaxLevel)
			return 0;

		return experienceTable.GetStartExpForLevel(level + 1) - experienceTable.GetStartExpForLevel(level);
	}
}
