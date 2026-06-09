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
        public static readonly Predicate<Aion.GameServer.Model.GameObjects.Player.Player> ONLINE = player => player.IsOnline();

        public static readonly Predicate<Aion.GameServer.Model.GameObjects.Player.Player> WITH_LOOT_PET = player => player.GetPet() != null
            && player.GetPet().GetObjectTemplate().ContainsFunction(PetFunctionType.LOOT);

        public static Predicate<Aion.GameServer.Model.GameObjects.Player.Player> SameRace(Aion.GameServer.Model.GameObjects.Player.Player p)
        {
            return player => p.GetRace() == player.GetRace();
        }

        public static Predicate<Aion.GameServer.Model.GameObjects.Player.Player> AllExcept(Aion.GameServer.Model.GameObjects.Player.Player ignored)
        {
            return player => !player.Equals(ignored);
        }

        public static Predicate<Aion.GameServer.Model.GameObjects.Player.Player> CanBeMentoredBy(Aion.GameServer.Model.GameObjects.Player.Player mentor)
        {
            return player => player.GetLevel() + 10 <= mentor.GetLevel();
        }
    }
}
