using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class StaticDataNpcSkillTests
{
	[Fact]
	public async Task LoadFromCacheAsync_ProjectsNpcSkillSpawnXmlDefaultsAndNpcIdIndex()
	{
		using var temp = TempDirectory.Create();
		var cacheFile = Path.Combine(temp.Path, "static_data.xml");
		File.WriteAllText(
			cacheFile,
			"""
			<static_data>
				<npc_skill_templates>
					<npc_skills npc_ids="1001 1002	1003">
						<npc_skill id="2001" lv="3" prob="75" is_post_spawn="true">
							<cond/>
							<spawn_npc npc_id="3001"/>
						</npc_skill>
						<npc_skill id="2002" lv="4" prob="80" min_hp="10" max_hp="90" min_time="5" max_time="15" conjunction="OR" cd="30" prio="2" next_skill_time="7000" next_chain_id="9" chain_id="8" max_chain_time="12000" target="RANDOM">
							<cond cond_type="TARGET_IS_IN_RANGE" hp_below="35" range="18" npc_id="212362" delay="750" can_die="false" despawn_time="1200"/>
							<spawn_npc npc_id="3002" delay="11" min_distance="2" max_distance="6" min_count="3" max_count="5"/>
						</npc_skill>
					</npc_skills>
					<npc_skills npc_ids="1002 1004">
						<npc_skill id="2999" lv="1" prob="100"/>
					</npc_skills>
				</npc_skill_templates>
			</static_data>
			""");

		var staticData = await StaticData.LoadFromCacheAsync(cacheFile, Array.Empty<string>());

		Assert.Equal(4, staticData.NpcSkills.Count);
		var firstList = staticData.NpcSkills.GetNpcSkillList(1001)!;
		Assert.Same(firstList, staticData.NpcSkills.GetNpcSkillList(1002));
		Assert.Same(firstList, staticData.NpcSkills.GetNpcSkillList(1003));
		Assert.NotSame(firstList, staticData.NpcSkills.GetNpcSkillList(1004));
		Assert.Equal([1001, 1002, 1003], firstList.NpcIds);

		var defaulted = firstList.Skills[0];
		Assert.Equal(2001, defaulted.SkillId);
		Assert.Equal(3, defaulted.SkillLevel);
		Assert.Equal(75, defaulted.Probability);
		Assert.Equal(0, defaulted.MinHp);
		Assert.Equal(100, defaulted.MaxHp);
		Assert.Equal(0, defaulted.MinTime);
		Assert.Equal(0, defaulted.MaxTime);
		Assert.Equal("AND", defaulted.Conjunction);
		Assert.Equal(0, defaulted.Cooldown);
		Assert.True(defaulted.IsPostSpawn);
		Assert.Equal(0, defaulted.Priority);
		Assert.Equal(-1, defaulted.NextSkillTime);
		Assert.Equal(0, defaulted.NextChainId);
		Assert.Equal(0, defaulted.ChainId);
		Assert.Equal(15000, defaulted.MaxChainTime);
		Assert.Equal("MOST_HATED", defaulted.Target);
		Assert.Equal(new NpcSkillSpawnSummary(3001, 0, 0, 0, 1, 0), defaulted.Spawn);
		Assert.Equal(new NpcSkillConditionSummary("NONE", 50, 10, 0, 0, true, 500), defaulted.Condition);

		var explicitSpawn = firstList.Skills[1];
		Assert.Equal(2002, explicitSpawn.SkillId);
		Assert.Equal(10, explicitSpawn.MinHp);
		Assert.Equal(90, explicitSpawn.MaxHp);
		Assert.Equal(5, explicitSpawn.MinTime);
		Assert.Equal(15, explicitSpawn.MaxTime);
		Assert.Equal("OR", explicitSpawn.Conjunction);
		Assert.Equal(30, explicitSpawn.Cooldown);
		Assert.Equal(2, explicitSpawn.Priority);
		Assert.Equal(7000, explicitSpawn.NextSkillTime);
		Assert.Equal(9, explicitSpawn.NextChainId);
		Assert.Equal(8, explicitSpawn.ChainId);
		Assert.Equal(12000, explicitSpawn.MaxChainTime);
		Assert.Equal("RANDOM", explicitSpawn.Target);
		Assert.Equal(new NpcSkillSpawnSummary(3002, 11, 2, 6, 3, 5), explicitSpawn.Spawn);
		Assert.Equal(
			new NpcSkillConditionSummary("TARGET_IS_IN_RANGE", 35, 18, 212362, 750, false, 1200),
			explicitSpawn.Condition);
	}

	private sealed class TempDirectory : IDisposable
	{
		private TempDirectory(string path)
		{
			Path = path;
		}

		public string Path { get; }

		public static TempDirectory Create()
		{
			var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion-npc-skills-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(path);
			return new TempDirectory(path);
		}

		public void Dispose()
		{
			if (Directory.Exists(Path))
				Directory.Delete(Path, recursive: true);
		}
	}
}
