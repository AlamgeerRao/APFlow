namespace APFlow.Domain.Common.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist. Mapped to HTTP 404 by the
/// API's global exception handling middleware.
/// </summary>
public sealed class NotFoundException : AppFlowException
{
    /// <summary>Creates a new <see cref="NotFoundException"/> with the given client-safe message.</summary>
    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Creates a new <see cref="NotFoundException"/> describing a missing resource by type and key.</summary>
    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} with key '{key}' was not found.")
    {
    }
}
