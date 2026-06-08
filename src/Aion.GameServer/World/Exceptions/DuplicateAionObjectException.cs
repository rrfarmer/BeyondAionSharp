using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.World.Exceptions;

/// <summary>Java parity: world/exceptions/DuplicateAionObjectException.</summary>
public class DuplicateAionObjectException : Exception
{
    public DuplicateAionObjectException(AionObject obj, AionObject presentObject)
        : base(CreateMessage(obj, presentObject)) { }

    private static string CreateMessage(AionObject obj, AionObject presentObject)
    {
        // TODO-backlog: append Player.GetPosition() when Player extends VisibleObject (F4)
        return $"Duplicate object: {obj}, already present object: {presentObject}";
    }
}
