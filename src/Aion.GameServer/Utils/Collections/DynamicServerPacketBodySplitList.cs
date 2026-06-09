using System;
using System.Collections.Generic;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Utils.Collections;

/// <summary>
/// Java parity: utils/collections/DynamicServerPacketBodySplitList (Sykra, Neon).
/// A more specialized version of DynamicElementCountSplitList. It will utilize the maximum server packet body
/// byte size to determine how many elements can be included in a partition. The maximum usable byte size is
/// dynamically determined by the MAX_BODY_SIZE and the given static body byte length.
/// Function&lt;Type,Integer&gt;→Func&lt;TType,int&gt;. AionServerPacket (gameserver network pillar) red-tolerated.
/// </summary>
/// <typeparam name="TType">element type (Java type-parameter name was Type)</typeparam>
public class DynamicServerPacketBodySplitList<TType> : DynamicElementCountSplitList<TType>
{
    /// <param name="listToSplit">List of elements to split</param>
    /// <param name="oneTimeSplitOnEmptyData">true if an empty list should produce one split with an empty list</param>
    /// <param name="staticBodyByteSize">static server packet body size, that will be subtracted from the maximum body size to determine the usable body size in bytes</param>
    /// <param name="byteLengthCalculator">Func that will calculate the byte length of one element</param>
    public DynamicServerPacketBodySplitList(List<TType> listToSplit, bool oneTimeSplitOnEmptyData, int staticBodyByteSize,
        Func<TType, int> byteLengthCalculator)
        : base(listToSplit, oneTimeSplitOnEmptyData, AionServerPacket.MAX_USABLE_PACKET_BODY_SIZE - staticBodyByteSize, byteLengthCalculator)
    {
    }
}
