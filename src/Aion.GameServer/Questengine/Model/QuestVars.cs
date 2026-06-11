using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.QuestEngine.Model;

/// <summary>Java parity: questEngine/model/QuestVars (MrPoke).</summary>
public class QuestVars
{
    private static readonly ILogger log = NullLogger.Instance;

    private readonly int[] questVars = new int[6];

    public QuestVars()
    {
    }

    public QuestVars(int var)
    {
        SetVar(var);
    }

    /// <returns>Quest var by id.</returns>
    public int GetVarById(int id)
    {
        return questVars[id];
    }

    public void SetVarById(int id, int var)
    {
        if (var > 0x3F)
            log.LogWarning(new System.ArgumentException(), "Out of range value was passed for quest var on index " + id);
        questVars[id] = var;
    }

    /// <returns>int value of all values stored in the array. Representation: Sum(value_on_index_i * 64^i).</returns>
    public int GetQuestVars()
    {
        int var = 0;
        for (int i = 5; i >= 0; i--)
        {
            var <<= 0x06;
            var |= questVars[i];
        }
        return var;
    }

    /// <summary>Fill the array with values, based on an int value represented like GetQuestVars().</summary>
    public void SetVar(int var)
    {
        for (int i = 0; i <= 5; i++)
        {
            questVars[i] = var & 0x3F;
            var >>= 0x06;
        }
    }
}
