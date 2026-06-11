using System;
using System.Text;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.World.Exceptions;

/// <summary>Java parity: world/exceptions/DuplicateAionObjectException (RuntimeException→Exception).</summary>
public class DuplicateAionObjectException : Exception
{
    public DuplicateAionObjectException(AionObject obj, AionObject presentObject)
        : base(CreateMessage(obj, presentObject))
    {
    }

    private static string CreateMessage(AionObject obj, AionObject presentObject)
    {
        StringBuilder sb = new StringBuilder("Duplicate object: ");
        sb.Append(obj);
        if (obj is Player)
            sb.Append(' ').Append(((Player) obj).GetPosition());
        sb.Append(", already present object: ");
        sb.Append(presentObject);
        if (presentObject is Player)
            sb.Append(' ').Append(((Player) presentObject).GetPosition());
        return sb.ToString();
    }
}
