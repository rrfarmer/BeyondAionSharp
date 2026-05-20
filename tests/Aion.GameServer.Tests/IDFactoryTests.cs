using System.Collections.Concurrent;
using Aion.GameServer.Utils.IdFactory;

namespace Aion.GameServer.Tests;

public class IDFactoryTests
{
	[Fact]
	public void NextId_AllocatesSequentialIdsAfterReservedZero()
	{
		var factory = new IDFactory();

		Assert.Equal(1, factory.GetUsedCount());
		Assert.Equal(1, factory.NextId());
		Assert.Equal(2, factory.NextId());
		Assert.Equal(3, factory.GetUsedCount());
	}

	[Fact]
	public void NextId_SkipsPreloadedIds()
	{
		var factory = new IDFactory([1, 2, 3]);

		Assert.Equal(4, factory.NextId());
	}

	[Fact]
	public void NextId_SkipsIdsThatAreInvisibleToTheClient()
	{
		var factory = new IDFactory(Enumerable.Range(1, 6483));

		Assert.True(IDFactory.IsInvalidId(6484));
		Assert.True(IDFactory.IsInvalidId(6485));
		Assert.True(IDFactory.IsInvalidId(6486));
		Assert.True(IDFactory.IsInvalidId(6487));
		Assert.Equal(6488, factory.NextId());
	}

	[Fact]
	public void ReleaseId_MakesLowerIdAvailableAgain()
	{
		var factory = new IDFactory();
		var first = factory.NextId();
		var second = factory.NextId();

		Assert.True(factory.ReleaseId(first));

		Assert.Equal(first, factory.NextId());
		Assert.Equal(2, second);
	}

	[Fact]
	public void ReleaseId_ReturnsFalseForUnknownId()
	{
		var factory = new IDFactory();

		Assert.False(factory.ReleaseId(500));
	}

	[Fact]
	public void Constructor_RejectsDuplicatePreloadedIds()
	{
		Assert.Throws<IDFactoryException>(() => new IDFactory([1, 1]));
	}

	[Fact]
	public void LockIds_ReservesIdsAfterConstruction()
	{
		var factory = new IDFactory();

		factory.LockIds([1, 2, 3]);

		Assert.Equal(4, factory.NextId());
	}

	[Fact]
	public void LockIds_RejectsDuplicateRuntimeIds()
	{
		var factory = new IDFactory();

		factory.LockIds([1]);

		Assert.Throws<IDFactoryException>(() => factory.LockIds([1]));
	}

	[Fact]
	public void NextId_IsThreadSafe()
	{
		var factory = new IDFactory();
		var ids = new ConcurrentBag<int>();

		Parallel.For(0, 1000, _ => ids.Add(factory.NextId()));

		Assert.Equal(1000, ids.Count);
		Assert.Equal(1000, ids.Distinct().Count());
		Assert.DoesNotContain(0, ids);
	}
}
