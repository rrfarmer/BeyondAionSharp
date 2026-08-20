using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Pattern;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// The answering half of the guard call family, for npcs whose own class had no <c>on_message</c>.
/// </summary>
/// <remarks>
/// 102 artifact protectors answer <c>23100</c> in retail and none of them could here: the class carries
/// a call and a death announcement and never listened for anything. Measured with
/// <c>tools/client-extract/extract_guard_answers.py --gaps</c>, which also shows <c>23200</c> is already
/// fully bound and that the remaining <c>23000</c> shortfall is 24 npcs on five bespoke classes.
/// </remarks>
[Collection("GoldenDataManager")]
public sealed class GuardAnswersTests
{
	private const int Reshanta = 400010000;

	/// <summary>A dread remnant lieutenant: an artifact protector that answers 23100.</summary>
	private const int Protector = 251450;

	private static BossAiHarness NewHarness() =>
		BossAiHarness.For(Reshanta).WithWorldSize(4096)
			.WithAi(typeof(ArtifactProtectorAI), typeof(GarrisonGuardCallAI), typeof(AggressiveNpcAI),
				typeof(GeneralNpcAI))
			.Build();

	/// <summary><b>An artifact protector now hears the garrison call and takes hate from it.</b></summary>
	[Fact]
	public void AnArtifactProtectorAnswersTheGarrisonCall()
	{
		using BossAiHarness harness = NewHarness();
		Npc crier = harness.Spawn(Protector, 300f, 300f, 200f);
		Npc listener = harness.Spawn(Protector, 320f, 300f, 200f);
		Player player = harness.SpawnPlayer(318f, 300f, 200f, race: Race.ELYOS);
		BossAiHarness.MakeMutuallyKnown(crier, listener);
		BossAiHarness.MakeMutuallyKnown(listener, player);

		NpcMessageBus.Broadcast(crier, GarrisonGuardCallAI.ThisOne, player, 25f);

		// Retail's idle rung: one point, and go for whoever it now hates most.
		Assert.Equal(1, listener.GetAggroList().GetHate(player));
	}

	/// <summary><b>And a protector that answers nothing in retail still hears nothing.</b></summary>
	[Fact]
	public void AProtectorOutsideTheTableStaysDeaf()
	{
		Assert.Empty(GuardAnswers.RungsFor(-1));
	}

	/// <summary>
	/// <b>The fighting rung is emitted before the idle one.</b> Their conditions differ only by
	/// <c>When.Fighting</c>, so the idle rung would swallow every call if it came first.
	/// </summary>
	[Fact]
	public void TheFightingRungOutranksTheIdleOne()
	{
		PatternBranch[] rungs = GuardAnswers.RungsFor(Protector);

		Assert.Equal(2, rungs.Length);
		Assert.True(rungs[0].Priority > rungs[1].Priority);
	}

	/// <summary>
	/// <b>Every answer in the table carries retail's two rungs, or says why not.</b> Four npcs answer
	/// with a thousand points and no fighting rung at all; the rest are the uniform 1/100 pair.
	/// </summary>
	[Fact]
	public void TheTableIsRetailsTwoRungsAndItsFourExceptions()
	{
		int odd = 0;
		foreach ((int _, GuardAnswers.Answer[] answers) in GuardAnswers.ByNpc)
		{
			foreach (GuardAnswers.Answer answer in answers)
			{
				Assert.True(answer.Idle >= 0);
				if (answer.Idle != 1 || answer.Busy != 100)
					odd++;
			}
		}

		Assert.Equal(4, odd);
	}
}
