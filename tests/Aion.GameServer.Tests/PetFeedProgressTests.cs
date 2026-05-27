using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Tests;

public sealed class PetFeedProgressTests
{
	[Fact]
	public void GetDataForPacketPacksJavaBitFields()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 10);
		progress.SetRegularCount(0x12);
		progress.TotalPoints = 0x3454;
		for (var i = 0; i < 0x15; i++)
		{
			progress.IncrementCount(lovedFood: true);
		}

		Assert.Equal(0x12345550, progress.GetDataForPacket());
	}

	[Fact]
	public void SetDataDecodesJavaSavedDataAndReencodesWithPointLowBitsCleared()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 40);

		progress.SetData(0x12345550);

		Assert.Equal(0x12, progress.RegularCount);
		Assert.Equal(40 - 0x15, progress.LovedFoodRemaining);
		Assert.Equal(0x12345550, progress.GetDataForPacket());
	}

	[Fact]
	public void TotalPointsMasksToJavaFourteenBits()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0);

		progress.TotalPoints = 0x7FFF;

		Assert.Equal(0x3FFF, progress.TotalPoints);
		Assert.Equal(0x3FFC00, progress.GetDataForPacket());
	}

	[Fact]
	public void RegularCountPreservesJavaUnsignedByteView()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0);

		progress.SetRegularCount(unchecked((short)0xFFEE));

		Assert.Equal(0xEE, progress.RegularCount);
	}

	[Fact]
	public void ResetPreservesJavaLovedFeededOneShotBehavior()
	{
		var progress = new PetFeedProgress(lovedFoodLimit: 0);
		progress.TotalPoints = 100;
		progress.SetRegularCount(4);
		progress.SetIsLovedFeeded();

		progress.Reset();

		Assert.False(progress.IsLovedFeeded);
		Assert.Equal(100, progress.TotalPoints);
		Assert.Equal(4, progress.RegularCount);

		progress.Reset();

		Assert.Equal(0, progress.TotalPoints);
		Assert.Equal(0, progress.RegularCount);
	}

	[Fact]
	public void PetHungryLevelCyclesLikeJava()
	{
		Assert.Equal(PetHungryLevel.Content, PetHungryLevel.Hungry.GetNextValue());
		Assert.Equal(PetHungryLevel.Semifull, PetHungryLevel.Content.GetNextValue());
		Assert.Equal(PetHungryLevel.Full, PetHungryLevel.Semifull.GetNextValue());
		Assert.Equal(PetHungryLevel.Hungry, PetHungryLevel.Full.GetNextValue());
		Assert.Equal(PetHungryLevel.Semifull, PetHungryLevelExtensions.FromId(2));
		Assert.Throws<ArgumentOutOfRangeException>(() => PetHungryLevelExtensions.FromId(4));
	}
}
