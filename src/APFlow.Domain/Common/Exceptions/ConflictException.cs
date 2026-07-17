namespace APFlow.Domain.Common.Exceptions;

/// <summary>
/// Thrown when an operation conflicts with the current state of a resource
/// (e.g. a concurrency conflict or a duplicate). Mapped to HTTP 409 by the
/// API's global exception handling middleware.
/// </summary>
public sealed class ConflictException : AppFlowException
{
    /// <summary>Creates a new <see cref="ConflictException"/> with the given client-safe message.</summary>
    public ConflictException(string message)
        : base(message)
    {
    }
}
