using System;
using System.IO;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>
/// Java parity: transformers/FileTransformer (SoulKeeper). Transforms a string to a file by creating a new file
/// instance (existence is not checked). Java's <c>java.io.File</c> maps to C# <see cref="FileInfo"/>.
/// </summary>
public sealed class FileTransformer : PropertyTransformer
{
    public override bool Matches(Type targetType) => targetType == typeof(FileInfo);

    protected override object? ParseObject(string value, Type type) => new FileInfo(value);
}
