namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Something with a DB-persistence state.
/// Java parity: model/gameobjects/Persistable.
/// </summary>
public interface IPersistable
{
    static readonly Func<IPersistable?, bool> NEW = NewPredicate(PersistentState.NEW);
    static readonly Func<IPersistable?, bool> CHANGED = NewPredicate(PersistentState.UPDATE_REQUIRED);
    static readonly Func<IPersistable?, bool> DELETED = NewPredicate(PersistentState.DELETED);

    // Java parity: getPersistentState()
    PersistentState GetPersistentState();

    // Java parity: setPersistentState(PersistentState)
    void SetPersistentState(PersistentState state);

    // Java parity: static newPredicate(PersistentState)
    static Func<IPersistable?, bool> NewPredicate(PersistentState state) =>
        persistable => persistable != null && persistable.GetPersistentState() == state;

    // Java parity: nested enum Persistable.PersistentState
    enum PersistentState
    {
        NEW,
        UPDATE_REQUIRED,
        UPDATED,
        DELETED,
        NOACTION,
    }
}
