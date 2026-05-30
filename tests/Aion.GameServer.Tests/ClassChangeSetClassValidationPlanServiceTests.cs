using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class ClassChangeSetClassValidationPlanServiceTests
{
	[Fact]
	public void CreatePlan_NullNewClassAlwaysBlocks()
	{
		var plan = ClassChangeSetClassValidationPlanService.CreatePlan("WARRIOR", newClass: null, validate: true);

		Assert.Equal(ClassChangeSetClassValidationPlanStatus.BlockedNullNewClass, plan.Status);
		Assert.False(plan.IsValid);
		Assert.False(plan.IsLive);
	}

	[Fact]
	public void CreatePlan_ValidateFalseSkipsAllValidation()
	{
		// Java: if (!validate) skip the checks
		var plan = ClassChangeSetClassValidationPlanService.CreatePlan("WARRIOR", "GLADIATOR", validate: false);

		Assert.Equal(ClassChangeSetClassValidationPlanStatus.SkippedValidationDisabled, plan.Status);
		Assert.True(plan.IsValid);
	}

	[Theory]
	[InlineData("GLADIATOR")]
	[InlineData("TEMPLAR")]
	[InlineData("ASSASSIN")]
	[InlineData("SORCERER")]
	public void CreatePlan_AlreadySwitchedClassBlocks(string nonStartingClass)
	{
		// Java: if (!oldClass.isStartingClass()) -> "You already switched class"
		var plan = ClassChangeSetClassValidationPlanService.CreatePlan(nonStartingClass, "GLADIATOR", validate: true);

		Assert.Equal(ClassChangeSetClassValidationPlanStatus.BlockedAlreadySwitchedClass, plan.Status);
		Assert.False(plan.IsValid);
		Assert.Contains("already switched", plan.JavaSource.ToLowerInvariant());
	}

	[Theory]
	[InlineData("WARRIOR", "GLADIATOR")]
	[InlineData("WARRIOR", "TEMPLAR")]
	[InlineData("SCOUT", "ASSASSIN")]
	[InlineData("SCOUT", "RANGER")]
	[InlineData("MAGE", "SORCERER")]
	[InlineData("MAGE", "SPIRIT_MASTER")]
	[InlineData("PRIEST", "CLERIC")]
	[InlineData("PRIEST", "CHANTER")]
	[InlineData("ENGINEER", "RIDER")]
	[InlineData("ENGINEER", "GUNNER")]
	[InlineData("ARTIST", "BARD")]
	public void CreatePlan_ValidStartingClassToSubclassIsAllowed(string oldClass, string newClass)
	{
		var plan = ClassChangeSetClassValidationPlanService.CreatePlan(oldClass, newClass, validate: true);

		Assert.Equal(ClassChangeSetClassValidationPlanStatus.Valid, plan.Status);
		Assert.True(plan.IsValid);
	}

	[Fact]
	public void CreatePlan_SameClassBlocks()
	{
		var plan = ClassChangeSetClassValidationPlanService.CreatePlan("WARRIOR", "WARRIOR", validate: true);

		Assert.Equal(ClassChangeSetClassValidationPlanStatus.BlockedInvalidClassChoice, plan.Status);
		Assert.False(plan.IsValid);
	}

	[Theory]
	[InlineData("WARRIOR", "SCOUT")]   // 0 -> 3, out of range (3 > 2)
	[InlineData("WARRIOR", "SORCERER")] // 0 -> 7, out of range
	[InlineData("SCOUT", "WARRIOR")]   // 3 -> 0, too low
	[InlineData("MAGE", "GLADIATOR")]  // 6 -> 1, too low
	public void CreatePlan_OutOfRangeSubclassBlocks(string oldClass, string newClass)
	{
		var plan = ClassChangeSetClassValidationPlanService.CreatePlan(oldClass, newClass, validate: true);

		Assert.Equal(ClassChangeSetClassValidationPlanStatus.BlockedInvalidClassChoice, plan.Status);
		Assert.False(plan.IsValid);
	}

	[Fact]
	public void CreatePlan_IsCaseInsensitive()
	{
		var plan = ClassChangeSetClassValidationPlanService.CreatePlan("warrior", "gladiator", validate: true);

		Assert.Equal(ClassChangeSetClassValidationPlanStatus.Valid, plan.Status);
	}
}
