namespace APFlow.Domain.Common;

/// <summary>
/// Represents a single, structured failure reason attached to a <see cref="Result"/>
/// or <see cref="Result{T}"/>. Intentionally free of any business meaning at this
/// layer — individual features define their own error codes/messages using this shape.
/// </summary>
/// <param name="Code">A short, stable, machine-readable identifier (e.g. "NotFound", "Validation.Required").</param>
/// <param name="Message">A human-readable description of the failure.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>
    /// Represents the absence of an error. Used internally by successful <see cref="Result"/> instances.
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty);
}
