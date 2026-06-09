using System.Threading;

namespace Aion.GameServer.Utils;

/// <summary>
/// An int value that may be updated atomically.
/// C# equivalent of java.util.concurrent.atomic.AtomicInteger (Interlocked-backed).
/// </summary>
public sealed class AtomicInteger
{
    private int _value;

    public AtomicInteger(int initialValue = 0)
    {
        _value = initialValue;
    }

    // Java parity: get()
    public int Get() => Volatile.Read(ref _value);

    // Java parity: set(int)
    public void Set(int newValue) => Volatile.Write(ref _value, newValue);

    // Java parity: getAndSet(int)
    public int GetAndSet(int newValue) => Interlocked.Exchange(ref _value, newValue);

    // Java parity: compareAndSet(int expect, int update)
    public bool CompareAndSet(int expect, int update) => Interlocked.CompareExchange(ref _value, update, expect) == expect;

    // Java parity: incrementAndGet()
    public int IncrementAndGet() => Interlocked.Increment(ref _value);

    // Java parity: decrementAndGet()
    public int DecrementAndGet() => Interlocked.Decrement(ref _value);

    // Java parity: getAndIncrement()
    public int GetAndIncrement() => Interlocked.Increment(ref _value) - 1;

    // Java parity: getAndDecrement()
    public int GetAndDecrement() => Interlocked.Decrement(ref _value) + 1;

    // Java parity: addAndGet(int)
    public int AddAndGet(int delta) => Interlocked.Add(ref _value, delta);

    // Java parity: getAndAdd(int)
    public int GetAndAdd(int delta) => Interlocked.Add(ref _value, delta) - delta;

    // Java parity: intValue()
    public int IntValue() => Get();

    // Java parity: toString()
    public override string ToString() => Get().ToString();
}
