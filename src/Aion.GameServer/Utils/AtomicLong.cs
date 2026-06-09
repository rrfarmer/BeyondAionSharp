using System.Threading;

namespace Aion.GameServer.Utils;

/// <summary>
/// A long value that may be updated atomically.
/// C# equivalent of java.util.concurrent.atomic.AtomicLong (Interlocked-backed).
/// </summary>
public sealed class AtomicLong
{
    private long _value;

    public AtomicLong(long initialValue = 0)
    {
        _value = initialValue;
    }

    // Java parity: get()
    public long Get() => Interlocked.Read(ref _value);

    // Java parity: set(long)
    public void Set(long newValue) => Interlocked.Exchange(ref _value, newValue);

    // Java parity: getAndSet(long)
    public long GetAndSet(long newValue) => Interlocked.Exchange(ref _value, newValue);

    // Java parity: compareAndSet(long expect, long update)
    public bool CompareAndSet(long expect, long update) => Interlocked.CompareExchange(ref _value, update, expect) == expect;

    // Java parity: incrementAndGet()
    public long IncrementAndGet() => Interlocked.Increment(ref _value);

    // Java parity: decrementAndGet()
    public long DecrementAndGet() => Interlocked.Decrement(ref _value);

    // Java parity: getAndIncrement()
    public long GetAndIncrement() => Interlocked.Increment(ref _value) - 1;

    // Java parity: getAndDecrement()
    public long GetAndDecrement() => Interlocked.Decrement(ref _value) + 1;

    // Java parity: addAndGet(long)
    public long AddAndGet(long delta) => Interlocked.Add(ref _value, delta);

    // Java parity: getAndAdd(long)
    public long GetAndAdd(long delta) => Interlocked.Add(ref _value, delta) - delta;

    // Java parity: longValue()
    public long LongValue() => Get();

    // Java parity: intValue()
    public int IntValue() => (int) Get();

    // Java parity: toString()
    public override string ToString() => Get().ToString();
}
