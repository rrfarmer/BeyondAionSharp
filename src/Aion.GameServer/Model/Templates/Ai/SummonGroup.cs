using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Ai;

/// <summary>Java parity: model/templates/ai/SummonGroup (xTz).</summary>
[XmlType("SummonGroup")]
public class SummonGroup
{
    [XmlAttribute("npcId")] private int npcId;
    [XmlAttribute("x")] private float x;
    [XmlAttribute("y")] private float y;
    [XmlAttribute("z")] private float z;
    [XmlAttribute("h")] private byte h;
    [XmlAttribute("minCount")] private int minCount = 1;
    [XmlAttribute("maxCount")] private int maxCount;
    [XmlAttribute("distance")] private float distance;
    [XmlAttribute("schedule")] private int schedule;

    // Java parity: afterUnmarshal(Unmarshaller, Object parent) — invoked by the AI-template loader.
    public void AfterUnmarshal(object parent)
    {
        if (minCount <= 0)
            throw new System.ArgumentException("minCount (" + minCount + ") for npc group " + npcId + " must be greater than zero");
        if (maxCount == 0)
            maxCount = minCount;
        else if (maxCount < minCount)
            throw new System.ArgumentException("maxCount (" + maxCount + ") for npc group " + npcId + " must be greater than minCount (" + minCount + ")");
    }

    public int GetNpcId()
    {
        return npcId;
    }

    public float GetX()
    {
        return x;
    }

    public float GetY()
    {
        return y;
    }

    public float GetZ()
    {
        return z;
    }

    public byte GetH()
    {
        return h;
    }

    public int GetMinCount()
    {
        return minCount;
    }

    public int GetMaxCount()
    {
        return maxCount;
    }

    public float GetDistance()
    {
        return distance;
    }

    public int GetSchedule()
    {
        return schedule;
    }
}
