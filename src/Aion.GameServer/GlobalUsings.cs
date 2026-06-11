// Java->C# unavoidable-divergence aliases (compile-convergence theme, 2026-06-11).
//
// Several gameplay types share their simple name with a sub-namespace of the same name
// (Java has e.g. package `...gameobjects.player` AND class `Player`; lowercase packages
// never collide, but C# namespaces and types are both PascalCase so they do). A global
// using-alias rebinds the bare type name to the real type project-wide, resolving the
// CS0118 ("namespace used like a type") / CS0246 clashes with zero churn. This is the
// authorized "alias allowed" fix for the namespace/type collision.

// Player class lives in namespace Aion.GameServer.Model.GameObjects.Players (FQN ...Player.Player),
// which is also a namespace -> alias the bare name to the type.
global using Player = Aion.GameServer.Model.GameObjects.Players.Player;
