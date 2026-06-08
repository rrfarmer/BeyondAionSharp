namespace Aion.GameServer.SkillEngine.Model;

/// <summary>Tracks a creature's current/previous chain skill and expiry. Java parity: skillengine/model/ChainSkills.</summary>
public class ChainSkills
{
    private ChainSkill _previousChainSkill = new("");
    private ChainSkill _chainSkill = new("");
    private long _expireTime;

    // Java parity: getPreviousChainSkill()
    public ChainSkill GetPreviousChainSkill() => _previousChainSkill;

    // Java parity: getCurrentChainSkill()
    public ChainSkill GetCurrentChainSkill() => _chainSkill;

    // Java parity: getCurrentChainCount(String)
    public int GetCurrentChainCount(string category) =>
        _chainSkill.GetCategory() == category ? _chainSkill.GetUseCount() : 0;

    // Java parity: updateChain(String, int)
    public void UpdateChain(string category, int duration)
    {
        if (_chainSkill.GetCategory().Length == 0)
            _chainSkill.SetCategory(category);
        else if (_chainSkill.GetCategory() != category)
        {
            _previousChainSkill = _chainSkill;
            _chainSkill = new ChainSkill(category);
        }

        _chainSkill.IncreaseUseCount();
        _expireTime = duration == 0 ? 0 : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + duration;
    }

    // Java parity: resetChain()
    public void ResetChain()
    {
        if (_chainSkill.GetCategory().Length != 0)
        {
            _expireTime = 0;
            _previousChainSkill.Clear();
            _chainSkill.Clear();
        }
    }

    // Java parity: isChainExpired()
    public bool IsChainExpired() => _expireTime > 0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > _expireTime;
}
