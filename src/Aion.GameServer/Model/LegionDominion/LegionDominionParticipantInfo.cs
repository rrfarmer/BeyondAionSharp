using System;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Model.LegionDominion;

/// <summary>Java parity: model/legionDominion/LegionDominionParticipantInfo (Yeats). java.sql.Timestamp→DateTimeOffset? (getTime()/1000→ToUnixTimeMilliseconds()/1000). Legion/LegionService red-tolerated.</summary>
public class LegionDominionParticipantInfo
{
    private int legionId;
    private int points;
    private int time;
    private DateTimeOffset? date;

    public int GetLegionId()
    {
        return legionId;
    }

    public int GetPoints()
    {
        return points;
    }

    public int GetTime()
    {
        return time;
    }

    public long GetDate()
    {
        return date != null ? date.Value.ToUnixTimeMilliseconds() / 1000 : 0;
    }

    public DateTimeOffset? GetDateAsTimeStamp()
    {
        return date;
    }

    public void SetLegionId(int legionId)
    {
        this.legionId = legionId;
    }

    public void SetPoints(int points)
    {
        this.points = points;
    }

    public void SetTime(int time)
    {
        this.time = time;
    }

    public void SetDate(DateTimeOffset? timestamp)
    {
        this.date = timestamp;
    }

    public string GetLegionName()
    {
        Aion.GameServer.Model.Team.Legion.Legion legion = LegionService.GetInstance().GetLegion(legionId);
        if (legion != null)
            return legion.GetName();
        return "NOT AVAILABLE";
    }
}
