using System;

namespace Aion.GameServer.Model.Templates.Pet;

/// <summary>
/// Java parity: model/templates/pet/PetFunctionType (IlBuono, Rolandas). Java enum with per-instance id(byte) +
/// isPlayerFunc fields → C# enum (distinct ordinals; APPEARANCE is a DISTINCT constant from FOOD even though both
/// have id 1) + extension accessors. Java byte id (incl. -1/-2) → sbyte.
/// </summary>
public enum PetFunctionType
{
    WAREHOUSE,
    FOOD,
    DOPING,
    LOOT,
    BUFF,
    MERCHANT,
    NONE,
    APPEARANCE,
    // non writable to packets
    BAG,
    WING
}

public static class PetFunctionTypeExtensions
{
    // Java parity: getId() — byte id.
    public static sbyte GetId(this PetFunctionType t) => t switch
    {
        PetFunctionType.WAREHOUSE => 0,
        PetFunctionType.FOOD => 1,
        PetFunctionType.DOPING => 2,
        PetFunctionType.LOOT => 3,
        PetFunctionType.BUFF => 4,
        PetFunctionType.MERCHANT => 5,
        PetFunctionType.NONE => 6,
        PetFunctionType.APPEARANCE => 1,
        PetFunctionType.BAG => -1,
        PetFunctionType.WING => -2,
        _ => throw new ArgumentOutOfRangeException(),
    };

    // Java parity: isPlayerFunction() — APPEARANCE/BAG/WING are non-player functions.
    public static bool IsPlayerFunction(this PetFunctionType t) => t switch
    {
        PetFunctionType.APPEARANCE => false,
        PetFunctionType.BAG => false,
        PetFunctionType.WING => false,
        _ => true,
    };
}
