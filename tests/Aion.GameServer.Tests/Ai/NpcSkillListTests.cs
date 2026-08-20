using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// What <c>SKILLI_INDEX_N</c> means: the per-npc ordered skill list, and the two facts that check it.
/// </summary>
/// <remarks>
/// This was the largest single blocker in the project -- retail names skills by index into an ordered
/// list that was recorded here as server-side data nobody had. It is in the 5.8 server dump:
/// <c>npcs.xml</c> carries each npc's <c>&lt;skills&gt;</c> block in order, and <c>skill_base.xml</c>
/// joins the names to ids.
/// <para>
/// <b>A resolver that produced merely plausible answers would be worthless</b>, because the thing it
/// replaces -- reading this port's own <c>npc_skills.xml</c> in order -- also produces plausible
/// answers, and is known to be wrong for some bosses. So these pin the two orders that were established
/// independently, before the dump was read, and both have to come out right.
/// </para>
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class NpcSkillListTests
{
	/// <summary>Tiamat's avatars, whose order this port gets wrong.</summary>
	private const int TiamatAvatar = 219365;

	/// <summary>Hameroon of Haramel.</summary>
	private const int Hameroon = 216922;

	private static IReadOnlyDictionary<int, IReadOnlyDictionary<int, int>> Lists()
	{
		string path = Path.Combine(BossAiHarness.RepoRoot(),
			"tools", "client-extract", "out", "npc_skill_lists.tsv");
		string[] lines = File.ReadAllLines(path);
		string[] header = lines[0].Split('\t');
		int npcAt = Array.IndexOf(header, "npc");
		int indexAt = Array.IndexOf(header, "index");
		int skillAt = Array.IndexOf(header, "skill");

		Dictionary<int, Dictionary<int, int>> byNpc = new Dictionary<int, Dictionary<int, int>>();
		foreach (string line in lines.Skip(1))
		{
			string[] fields = line.Split('\t');
			int npc = int.Parse(fields[npcAt]);
			if (!byNpc.TryGetValue(npc, out Dictionary<int, int>? list))
				byNpc[npc] = list = new Dictionary<int, int>();
			list[int.Parse(fields[indexAt])] = int.Parse(fields[skillAt]);
		}

		return byNpc.ToDictionary(pair => pair.Key,
			pair => (IReadOnlyDictionary<int, int>)pair.Value);
	}

	/// <summary><b>Tiamat's avatars, in the order the pattern's own comments proved.</b></summary>
	/// <remarks>
	/// Settled before the dump was read, from <c>stack=</c> names matching the branch comments:
	/// 0 is the power attack, 1 the area attack, 2 the handbind. <b>This port's
	/// <c>npc_skills.xml</c> lists them 20105, 20145, 20146</b> -- so reading that file by position
	/// gives 0=20105, which is the handbind, and an avatar that opens with a root instead of its
	/// heavy hit. That the dump disagrees with our file and agrees with the independent reading is
	/// the whole reason to trust it.
	/// </remarks>
	[Fact]
	public void TiamatsAvatarsMatchTheOrderProvedFromTheBranchComments()
	{
		IReadOnlyDictionary<int, int> list = Lists()[TiamatAvatar];

		Assert.Equal(20145, list[0]);
		Assert.Equal(20146, list[1]);
		Assert.Equal(20105, list[2]);
	}

	/// <summary><b>Hameroon's index 1, proved from the shout data.</b></summary>
	/// <remarks>
	/// Retail's shout rows carry <c>skill_no</c>, which is the index plus one, and Hameroon's
	/// <c>skill_no="2"</c> shout fires exactly where his pattern casts a self-buff and spawns his
	/// brainwashed adds. That put 19210 at index 1 without any of this data.
	/// </remarks>
	[Fact]
	public void HameroonsSelfBuffIsWhereTheShoutDataSaid()
	{
		Assert.Equal(19210, Lists()[Hameroon][1]);
	}

	/// <summary><b>Indices are dense from zero, which is what makes them indexable at all.</b></summary>
	/// <remarks>
	/// A gap would mean the extractor dropped an entry, and every index after the gap would silently
	/// name the wrong skill -- the failure mode that is invisible in a spot check and obvious here.
	/// </remarks>
	[Fact]
	public void EveryListRunsFromZeroWithNoHoles()
	{
		foreach ((int npc, IReadOnlyDictionary<int, int> list) in Lists())
		{
			for (int index = 0; index < list.Count; index++)
				Assert.True(list.ContainsKey(index),
					$"npc {npc} has no entry at index {index} of {list.Count}");
		}
	}
}
