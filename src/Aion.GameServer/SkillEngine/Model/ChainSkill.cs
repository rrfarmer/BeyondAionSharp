namespace Aion.GameServer.SkillEngine.Model;

/// <summary>One chain-skill category's activation state. Java parity: skillengine/model/ChainSkill.</summary>
public class ChainSkill
{
    private string _category;
    private int _useCount;
    private long _lastUseTime;

    public ChainSkill(string category)
    {
        _category = category;
    }

    public void Clear()
    {
        _category = "";
        _useCount = 0;
        _lastUseTime = 0;
    }

    public string GetCategory() => _category;
    public void SetCategory(string name) => _category = name;
    public int GetUseCount() => _useCount;

    public void IncreaseUseCount()
    {
        _useCount++;
        _lastUseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    // Java parity: getLastUseTime() — 0 if never.
    public long GetLastUseTime() => _lastUseTime;
}
