namespace Aion.GameServer.Commons.Utils.RetailRandom;

public enum RetailRandomRegeneration { None, WriteSecond312, WriteFirst312AndReset }

public enum RetailRandomStatus
{
    Completed, InvalidRequest, UnsupportedSourceIdentity, UnknownCurrentOwnership, MissingState,
    StateSizeMismatch, StateHashMismatch, StaleGeneration, IncompleteAtSeedProvider,
    SeedReturnNotBound, UnsupportedDescriptor, BudgetExceeded
}

public readonly record struct RetailRandomRawStep(
    uint IndexBefore, uint SelectedIndex, uint IndexAfter, ulong Value, RetailRandomRegeneration Regeneration);
