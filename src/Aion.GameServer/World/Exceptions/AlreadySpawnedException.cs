using System;
using System.Text;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.World.Exceptions;

/// <summary>Java parity: world/exceptions/AlreadySpawnedException (RuntimeException→Exception).</summary>
public class AlreadySpawnedException : Exception
{
    public AlreadySpawnedException(VisibleObject obj)
        : base(CreateMessage(obj))
    {
    }

    private static string CreateMessage(VisibleObject obj)
    {
        StringBuilder sb = new StringBuilder(obj.GetType().Name);
        sb.Append(" ");
        sb.Append(obj.GetName());
        if (obj.GetObjectTemplate() != null && !(obj is Player))
            sb.Append(" (ID: ").Append(obj.GetObjectTemplate().GetTemplateId()).Append(")");
        sb.Append(" is already spawned at ");
        sb.Append(obj.GetPosition());
        return sb.ToString();
    }
}
