namespace APFlow.Domain.Common.Exceptions;

/// <summary>
/// Base type for all AP Flow application-defined exceptions. Represents an exceptional,
/// unrecoverable condition rather than an expected validation/business-rule outcome
/// (use <see cref="Result"/> / <see cref="Result{TValue}"/> for those).
/// Layer-specific business exceptions should derive from this type rather than
/// throwing raw <see cref="System.Exception"/> instances.
/// </summary>
public abstract class AppFlowException : Exception
{
    /// <summary>Creates a new <see cref="AppFlowException"/> with the given client-safe message.</summary>
    protected AppFlowException(string message)
        : base(message)
    {
    }

    /// <summary>Creates a new <see cref="AppFlowException"/> with the given client-safe message and inner exception.</summary>
    protected AppFlowException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
