using System;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>
/// Java parity: controllers/observer/DeathObserver.
/// </summary>
public class DeathObserver : ActionObserver
{
    private readonly Action<Creature> _actionOnDeath;

    public DeathObserver(Action<Creature> actionOnDeath)
        : base(ObserverType.DEATH)
    {
        _actionOnDeath = actionOnDeath;
    }

    public override void Died(Creature lastAttacker)
    {
        _actionOnDeath(lastAttacker);
    }
}
