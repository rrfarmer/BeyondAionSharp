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
global using Effect = Aion.GameServer.SkillEngine.Model.Effect;
global using Item = Aion.GameServer.Model.GameObjects.Item;

// java.lang.Runnable has no ambient C# equivalent; ported 1:1 as a commons interface and
// surfaced project-wide so `implements Runnable` / `(Runnable)` / `where T : Runnable` resolve.
global using Runnable = Aion.Commons.Lang.Runnable;

// The L10n template interface was ported as IL10n (C# I-prefix), but Java-faithful code
// references the bare name `L10n` as a base type. Alias it (bare type-position only; the
// ChatUtil.L10n(int) method and GetL10n()/l10nId members are member-access and unaffected).
global using L10n = Aion.GameServer.Model.Templates.IL10n;

// Java parity: Collection.isEmpty() surfaced project-wide as an IEnumerable<T> extension.
global using Aion.GameServer.Utils.Extensions;
