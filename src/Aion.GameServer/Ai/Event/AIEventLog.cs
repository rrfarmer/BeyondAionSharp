using System.Collections.Generic;

namespace Aion.GameServer.Ai.Event;

/// <summary>
/// Java parity: ai/event/AIEventLog (ATracer). Bounded deque logging recent AIEventTypes. Java extends LinkedBlockingDeque&lt;AIEventType&gt;
/// (no C# equivalent) -> backed by LinkedList&lt;AIEventType&gt; with a capacity field; default ctor is unbounded (int.MaxValue, matching
/// LinkedBlockingDeque's no-arg ctor). synchronized offerFirst -> lock; serialVersionUID dropped (no Java serialization).
/// </summary>
public class AIEventLog : LinkedList<AIEventType>
{
    private readonly int capacity;

    public AIEventLog() : this(int.MaxValue)
    {
    }

    public AIEventLog(int capacity)
    {
        this.capacity = capacity;
    }

    public bool OfferFirst(AIEventType e)
    {
        lock (this)
        {
            if (Count >= capacity)
            {
                RemoveLast();
            }
            AddFirst(e);
            return true;
        }
    }
}
