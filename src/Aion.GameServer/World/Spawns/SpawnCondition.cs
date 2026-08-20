using System;
using System.Collections.Generic;
using System.Globalization;

namespace Aion.GameServer.World.Spawns;

/// <summary>
/// Retail's <c>extcondition</c>: the boolean expression that decides whether a spawn group exists.
/// </summary>
/// <remarks>
/// The reader half of the conditional spawn engine. A retail world gates its spawn groups on named
/// variables — <b>54,388 gates across 163 worlds, 7,146 distinct expressions</b> — and this port has
/// never had any of it, so every gated group is either always present or always absent.
/// <para>
/// <b>The grammar is small and this covers all of it.</b> Measured over every gate in the 5.8 dump:
/// <c>==</c> 68,163, <c>&amp;&amp;</c> 25,225, <c>&gt;</c> 10,237, <c>&gt;=</c> 4,677, <c>||</c> 2,756,
/// <c>&lt;</c> 2,314, <c>!=</c> 1,504, <c>&lt;=</c> 1,244, brackets, and integer literals with the
/// occasional negative. <b>There is no arithmetic</b> — no sums, no variable-to-variable comparisons.
/// One variable against one integer is the whole of it.
/// </para>
/// <para>
/// <b>An unknown variable reads as zero</b>, which is what makes a world usable before its writers
/// exist: <c>SpecialServer_Cond == 0</c> — the most common gate in the dump after its own negation —
/// holds, and the ordinary spawns appear.
/// </para>
/// <para>
/// <b>A name may carry a <c>[SAVE]</c> marker</b> — 175 expressions and 2,600 gate uses do — which
/// means the variable is persisted rather than reset with the world. The marker is kept as part of the
/// name, because <b>eighteen names appear both with and without it</b> (<c>v01</c>, <c>v05</c>,
/// <c>link_weapon_li</c> among them): stripping it would merge two variables retail keeps apart.
/// <see cref="PersistedVariables"/> reports which ones they are.
/// </para>
/// <para>
/// <b>A bare variable is a test for "not zero".</b> 101 gates carry no comparison at all.
/// </para>
/// <para>
/// <b>Eight gates in the dump are truncated by retail</b> and end mid-expression with an unclosed
/// bracket — <c>(1131_mistoff == 1) &amp;&amp; (1141_mistoff == 1</c> is one, in the raw world file, not
/// in this port's reading of it. They are refused rather than guessed at, and the twelve spawn groups
/// behind them are named in docs/retail-ai-fidelity.md.
/// </para>
/// </remarks>
public sealed class SpawnCondition
{
	private readonly Node root;

	private SpawnCondition(Node root)
	{
		this.root = root;
	}

	/// <summary>The expression this was parsed from, kept for diagnostics.</summary>
	public string Source { get; private init; } = string.Empty;

	/// <summary>Parses one <c>extcondition</c>, or throws <see cref="FormatException"/>.</summary>
	public static SpawnCondition Parse(string expression)
	{
		ArgumentNullException.ThrowIfNull(expression);

		var reader = new Reader(expression);
		Node parsed = reader.ReadOr();
		reader.SkipSpace();
		if (!reader.Done)
			throw new FormatException($"trailing text in spawn condition: {expression}");

		return new SpawnCondition(parsed) { Source = expression };
	}

	/// <summary>Whether the gate holds, given a variable store.</summary>
	/// <param name="values">
	/// Looked up by name. A variable the store does not carry reads as zero — see the class remark.
	/// </param>
	public bool Holds(IReadOnlyDictionary<string, int> values)
	{
		ArgumentNullException.ThrowIfNull(values);
		return root.Holds(values);
	}

	/// <summary>The variables this gate reads that retail marks <c>[SAVE]</c>, so a store can persist
	/// exactly those.</summary>
	public IReadOnlyCollection<string> PersistedVariables
	{
		get
		{
			var names = new HashSet<string>(StringComparer.Ordinal);
			root.Collect(names);
			names.RemoveWhere(name => !name.StartsWith(Persisted, StringComparison.Ordinal));
			return names;
		}
	}

	/// <summary>Retail's marker for a variable that survives the world.</summary>
	public const string Persisted = "[SAVE]";

	/// <summary>Every variable this gate reads, so a caller can know what to watch.</summary>
	public IReadOnlyCollection<string> Variables
	{
		get
		{
			var names = new HashSet<string>(StringComparer.Ordinal);
			root.Collect(names);
			return names;
		}
	}

	private abstract class Node
	{
		public abstract bool Holds(IReadOnlyDictionary<string, int> values);

		public abstract void Collect(HashSet<string> names);
	}

	private sealed class Both(Node left, Node right, bool either) : Node
	{
		public override bool Holds(IReadOnlyDictionary<string, int> values)
			=> either
				? left.Holds(values) || right.Holds(values)
				: left.Holds(values) && right.Holds(values);

		public override void Collect(HashSet<string> names)
		{
			left.Collect(names);
			right.Collect(names);
		}
	}

	private sealed class Compare(string name, string op, int value) : Node
	{
		public override bool Holds(IReadOnlyDictionary<string, int> values)
		{
			int actual = values.TryGetValue(name, out int found) ? found : 0;
			return op switch
			{
				"==" => actual == value,
				"!=" => actual != value,
				">" => actual > value,
				">=" => actual >= value,
				"<" => actual < value,
				"<=" => actual <= value,
				_ => throw new FormatException($"unknown operator {op}"),
			};
		}

		public override void Collect(HashSet<string> names) => names.Add(name);
	}

	private sealed class Reader(string text)
	{
		private int at;

		public bool Done => at >= text.Length;

		public void SkipSpace()
		{
			while (at < text.Length && char.IsWhiteSpace(text[at]))
				at++;
		}

		public Node ReadOr()
		{
			Node left = ReadAnd();
			while (Take("||"))
				left = new Both(left, ReadAnd(), either: true);

			return left;
		}

		private Node ReadAnd()
		{
			Node left = ReadTerm();
			while (Take("&&"))
				left = new Both(left, ReadTerm(), either: false);

			return left;
		}

		private Node ReadTerm()
		{
			SkipSpace();
			if (Take("("))
			{
				Node inner = ReadOr();
				if (!Take(")"))
					throw new FormatException($"unclosed bracket in spawn condition: {text}");

				return inner;
			}

			string name = ReadName();

			// 101 gates are a bare variable with no comparison at all -- `CHALLENGE_504` and its
			// neighbours. Retail reads those as "not zero", which is the only meaning that leaves them
			// doing anything: a gate that is always true would not be written 101 times.
			if (!PeeksAtOperator())
				return new Compare(name, "!=", 0);

			string op = ReadOperator();
			return new Compare(name, op, ReadNumber());
		}

		private string ReadName()
		{
			SkipSpace();
			int start = at;

			// `[SAVE]` marks a persisted variable and is part of the name, not decoration around it.
			if (Take(Persisted))
			{
				while (at < text.Length && (char.IsLetterOrDigit(text[at]) || text[at] == '_'))
					at++;

				if (at == start + Persisted.Length)
					throw new FormatException($"expected a variable after {Persisted}: {text}");

				return text[start..at];
			}

			while (at < text.Length && (char.IsLetterOrDigit(text[at]) || text[at] == '_'))
				at++;

			if (start == at)
				throw new FormatException($"expected a variable in spawn condition: {text}");

			return text[start..at];
		}

		private bool PeeksAtOperator()
		{
			int mark = at;
			SkipSpace();
			bool found = at < text.Length && (text[at] is '=' or '!' or '>' or '<');
			at = mark;
			return found;
		}

		private string ReadOperator()
		{
			SkipSpace();
			// The two-character forms first: reading `>` out of `>=` would leave `=` and fail later,
			// with a message pointing at the wrong place.
			foreach (string op in new[] { "==", "!=", ">=", "<=", ">", "<" })
			{
				if (Take(op))
					return op;
			}

			throw new FormatException($"expected a comparison in spawn condition: {text}");
		}

		private int ReadNumber()
		{
			SkipSpace();
			int start = at;
			if (at < text.Length && (text[at] == '-' || text[at] == '+'))
				at++;

			while (at < text.Length && char.IsDigit(text[at]))
				at++;

			if (!int.TryParse(text[start..at], NumberStyles.AllowLeadingSign,
					CultureInfo.InvariantCulture, out int value))
			{
				throw new FormatException($"expected a number in spawn condition: {text}");
			}

			return value;
		}

		private bool Take(string token)
		{
			SkipSpace();
			if (string.CompareOrdinal(text, at, token, 0, token.Length) != 0)
				return false;

			at += token.Length;
			return true;
		}
	}
}
