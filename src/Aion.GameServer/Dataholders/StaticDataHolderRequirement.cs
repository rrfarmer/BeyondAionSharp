namespace Aion.GameServer.Dataholders;

/// <summary>
/// Defines whether a leaf holder participates in the active Java <c>static_data.xml</c> import graph.
/// Java unmarshals that graph as one required object: any imported holder failure aborts the load. A
/// C#-only extension that is not imported by the active graph may fail open and preserve its prior value.
/// </summary>
internal enum StaticDataHolderRequirement
{
	RequiredImport,
	OptionalExtension,
}

internal sealed class StaticDataHolderLoadException : Exception
{
	public StaticDataHolderLoadException(Type holderType, string sourcePath, Exception innerException)
		: base($"Required static data holder {holderType.Name} failed to load from {sourcePath}.", innerException)
	{
		HolderType = holderType;
		SourcePath = sourcePath;
	}

	public Type HolderType { get; }

	public string SourcePath { get; }
}
