namespace Aion.Commons.Lang;

/// <summary>
/// Java parity: java.lang.Runnable. The functional interface implemented by any class whose
/// instances are intended to be executed by a thread; the class defines a parameterless method
/// named <c>run</c>. C# has no built-in equivalent ambient type (Action is a delegate, not an
/// interface that classes can implement and be constrained on), so the interface is ported 1:1
/// to preserve <c>implements Runnable</c>, <c>(Runnable) cast</c>, and <c>&lt;T extends Runnable&gt;</c>
/// usages verbatim (e.g. services/cron). Exposed project-wide via a global using alias.
/// </summary>
public interface Runnable
{
    void Run();
}
