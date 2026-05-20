namespace Aion.GameServer.Model.GameObjects;

public sealed record PlayerLifeStats(int CurrentHp, int CurrentMp, int CurrentFp)
{
	// Java parity: model/stats/container/CreatureLifeStats.setCurrentHp clamps to the calculated max HP.
	public int GetCurrentHp(int maxHp)
	{
		return Math.Clamp(CurrentHp, 0, maxHp);
	}

	// Java parity: CreatureLifeStats.setCurrentMp returns without changing MP when HP was loaded as dead.
	public int GetCurrentMp(int maxMp)
	{
		return CurrentHp <= 0 ? maxMp : Math.Clamp(CurrentMp, 0, maxMp);
	}

	// Java parity: model/stats/container/PlayerLifeStats.setCurrentFp clamps only below zero on DB restore.
	public int GetCurrentFp()
	{
		return Math.Max(0, CurrentFp);
	}
}
