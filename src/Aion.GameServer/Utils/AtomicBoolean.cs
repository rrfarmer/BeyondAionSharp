using System.Threading;

namespace Aion.GameServer.Utils;

/// <summary>
/// A boolean value that may be updated atomically.
/// C# equivalent of java.util.concurrent.atomic.AtomicBoolean (Interlocked-backed; 0=false,1=true).
/// </summary>
public sealed class AtomicBoolean
{
    private int _value;

    public AtomicBoolean(bool initialValue = false)
    {
        _value = initialValue ? 1 : 0;
    }

    // Java parity: get()
    public bool Get() => Volatile.Read(ref _value) != 0;

    // Java parity: set(boolean)
    public void Set(bool newValue) => Volatile.Write(ref _value, newValue ? 1 : 0);

    // Java parity: compareAndSet(boolean expect, boolean update)
    public bool CompareAndSet(bool expect, bool update)
    {
        int e = expect ? 1 : 0;
        int u = update ? 1 : 0;
        return Interlocked.CompareExchange(ref _value, u, e) == e;
    }

    // Java parity: getAndSet(boolean)
    public bool GetAndSet(bool newValue) => Interlocked.Exchange(ref _value, newValue ? 1 : 0) != 0;

    // Java parity: toString()
    public override string ToString() => Get() ? "true" : "false";
}
