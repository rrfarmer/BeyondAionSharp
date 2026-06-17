using System;

namespace Aion.Commons.Configuration;

/// <summary>Java parity: commons/configuration/TransformationException. Thrown when a property value cannot be coerced to the target type.</summary>
public class TransformationException : Exception
{
    public TransformationException(string message) : base(message) { }
    public TransformationException(string message, Exception cause) : base(message, cause) { }
}
