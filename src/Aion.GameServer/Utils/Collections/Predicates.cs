using System;
using Aion.GameServer.Model.Templates.Pet;

namespace Aion.GameServer.Utils.Collections;

/// <summary>Java parity: utils/collections/Predicates (ATracer, Neon). java.util.function.Predicate&lt;T&gt; → System.Predicate&lt;T&gt;.</summary>
public class Predicates
{
    private Predicates()
    {
    }

    public static Predicate<T> AlwaysTrue<T>()
    {
        return _ => true;
    }

    public static class Players
    {
        public static readonly Predicate<Aion.GameServer.Model.GameObjects.Players.Player> ONLINE = player => player.IsOnline();

        public static readonly Predicate<Aion.GameServer.Model.GameObjects.Players.Player> WITH_LOOT_PET = player => player.GetPet() != null
            && player.GetPet().GetObjectTemplate().ContainsFunction(PetFunctionType.LOOT);

        public static Predicate<Aion.GameServer.Model.GameObjects.Players.Player> SameRace(Aion.GameServer.Model.GameObjects.Players.Player p)
        {
            return player => p.GetRace() == player.GetRace();
        }

        public static Predicate<Aion.GameServer.Model.GameObjects.Players.Player> AllExcept(Aion.GameServer.Model.GameObjects.Players.Player ignored)
        {
            return player => !player.Equals(ignored);
        }

        public static Predicate<Aion.GameServer.Model.GameObjects.Players.Player> CanBeMentoredBy(Aion.GameServer.Model.GameObjects.Players.Player mentor)
        {
            return player => player.GetLevel() + 10 <= mentor.GetLevel();
        }
    }
}
