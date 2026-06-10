using System.Collections.Generic;
using System.Linq;

namespace Aion.GameServer.Ai;

/// <summary>
/// Java parity: ai/HpPhases. Tracks descending HP-percent phase thresholds for an NPC AI and fires handleHpPhase as each is crossed.
/// IntStream.concat(...).distinct() -> LINQ Concat().Distinct(); Comparator.reverseOrder() -> descending Sort; synchronized -> lock.
/// Generic bound &lt;T extends NpcAI &amp; PhaseHandler&gt; -> where T : NpcAI, PhaseHandler. NpcAI/AIState red-tolerated.
/// </summary>
public class HpPhases
{
    private readonly List<int> phaseHpPercents = new List<int>();
    private int currentPhase = 0;

    public HpPhases(int hpPercent, params int[] moreHpPercents)
    {
        foreach (int p in new[] { hpPercent }.Concat(moreHpPercents).Distinct())
            phaseHpPercents.Add(p);
        phaseHpPercents.Sort((a, b) => b.CompareTo(a)); // sort percents in descending order
    }

    public void Reset()
    {
        lock (phaseHpPercents)
        {
            currentPhase = 0;
        }
    }

    public void TryEnterNextPhase<T>(T ai) where T : NpcAI, PhaseHandler
    {
        if (!ai.GetOwner().IsSpawned() || ai.IsDead() || ai.IsInState(AIState.RETURNING))
            return;
        lock (phaseHpPercents)
        {
            if (currentPhase >= phaseHpPercents.Count)
                return;
            int phaseHpPercent = phaseHpPercents[currentPhase];
            if (phaseHpPercent >= ai.GetLifeStats().GetHpPercentage())
            {
                currentPhase++;
                ai.HandleHpPhase(phaseHpPercent);
            }
        }
    }

    public int GetCurrentPhase()
    {
        return currentPhase;
    }

    public interface PhaseHandler
    {
        void HandleHpPhase(int phaseHpPercent);
    }
}
