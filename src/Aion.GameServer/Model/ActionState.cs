namespace Aion.GameServer.Model;

/// <summary>
/// Java parity: model/ActionState. States the client puts into messages which name one, like STR_SKILL_CANT_CAST ("You cannot do that while you
/// are %0.") or STR_MSG_CANNOT_USE_ITEM_DURING_PATH_FLYING ("You cannot use an item while %0."). The comments show what each one reads as.
/// </summary>
public enum ActionState
{
    STANDING, // standing
    PATH_FLYING, // flying
    FREE_FLYING, // flying
    RIDING, // riding
    RESTING, // resting
    SITTING, // sitting
    DEAD, // dead
    FLY_DEAD, // dead
    PERSONAL_SHOP, // running a Private Store
    LOOTING, // looting
    FLY_LOOTING, // looting
    CURRENT_STATUS, // in your current status
    COMBAT, // in combat
    GLIDING, // gliding
    POLYMORPH, // Transformation Mode
}

// Java parity: enum ActionState implements L10n — C# enums can't implement IL10n, so provide the members as extensions.
public static class ActionStateExtensions
{
    public static int GetL10nId(this ActionState self)
    {
        switch (self)
        {
            case ActionState.STANDING: return 1400053;
            case ActionState.PATH_FLYING: return 1400054;
            case ActionState.FREE_FLYING: return 1400055;
            case ActionState.RIDING: return 1400056;
            case ActionState.RESTING: return 1400057;
            case ActionState.SITTING: return 1400058;
            case ActionState.DEAD: return 1400059;
            case ActionState.FLY_DEAD: return 1400060;
            case ActionState.PERSONAL_SHOP: return 1400061;
            case ActionState.LOOTING: return 1400062;
            case ActionState.FLY_LOOTING: return 1400063;
            case ActionState.CURRENT_STATUS: return 1400064;
            case ActionState.COMBAT: return 1400079;
            case ActionState.GLIDING: return 1400082;
            case ActionState.POLYMORPH: return 1401212;
            default: throw new System.ArgumentOutOfRangeException(nameof(self), self, null);
        }
    }

    // Java parity: L10n::getL10n() default method. Every ActionState id maps to a client string, so the lookup never yields null.
    public static string GetL10n(this ActionState self) => Utils.ChatUtil.L10n(self.GetL10nId())!;
}
