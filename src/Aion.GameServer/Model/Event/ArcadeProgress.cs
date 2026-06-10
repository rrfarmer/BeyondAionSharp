namespace Aion.GameServer.Model.Event;

/// <summary>Java parity: model/event/ArcadeProgress (Estrayl, AION 4.8). Plain holder.</summary>
public class ArcadeProgress
{
    private readonly int playerObjId;
    private int frenzyPoints;
    private int currentLevel;
    private long frenzyEndTimeMillis;
    private int resumeLevel;
    private long nextTryTimeMillis;

    public ArcadeProgress(int playerObjId)
    {
        this.playerObjId = playerObjId;
    }

    public int GetPlayerObjId()
    {
        return playerObjId;
    }

    public int GetFrenzyPoints()
    {
        return frenzyPoints;
    }

    public void SetFrenzyPoints(int frenzyPoints)
    {
        this.frenzyPoints = frenzyPoints;
    }

    public int GetCurrentLevel()
    {
        return currentLevel;
    }

    public void SetCurrentLevel(int currentLevel)
    {
        this.currentLevel = currentLevel;
    }

    public long GetFrenzyEndTimeMillis()
    {
        return frenzyEndTimeMillis;
    }

    public void SetFrenzyEndTimeMillis(long frenzyEndTimeMillis)
    {
        this.frenzyEndTimeMillis = frenzyEndTimeMillis;
    }

    public int GetResumeLevel()
    {
        return resumeLevel;
    }

    public void SetResumeLevel(int resumeLevel)
    {
        this.resumeLevel = resumeLevel;
    }

    public long GetNextTryTimeMillis()
    {
        return nextTryTimeMillis;
    }

    public void SetTimeNextTry(long nextTryTimeMillis)
    {
        this.nextTryTimeMillis = nextTryTimeMillis;
    }
}
