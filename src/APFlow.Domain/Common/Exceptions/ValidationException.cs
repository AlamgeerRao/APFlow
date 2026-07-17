namespace APFlow.Domain.Common.Exceptions;

/// <summary>
/// Thrown when input fails validation. Mapped to HTTP 400 by the API's global
/// exception handling middleware. Carries a set of field-level failures so the
/// client can render them without parsing the exception message.
/// </summary>
public sealed class ValidationException : AppFlowException
{
    /// <summary>Field name to failure-message(s) map.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Creates a new <see cref="ValidationException"/> with a single client-safe message and no field-level errors.</summary>
    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    /// <summary>Creates a new <see cref="ValidationException"/> carrying field-level validation errors.</summary>
    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}
