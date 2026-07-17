namespace APFlow.Domain.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated caller is not permitted to perform the requested
/// operation (e.g. a cross-tenant access attempt). Mapped to HTTP 403 by the
/// API's global exception handling middleware.
/// </summary>
public sealed class ForbiddenException : AppFlowException
{
    /// <summary>Creates a new <see cref="ForbiddenException"/> with the given client-safe message.</summary>
    public ForbiddenException(string message)
        : base(message)
    {
    }
}
