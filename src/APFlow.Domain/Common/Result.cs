namespace APFlow.Domain.Common;

/// <summary>
/// Represents the outcome of an operation that can succeed or fail, without relying on
/// exceptions for expected/control-flow failures. Use this for validation and business-rule
/// outcomes; reserve exceptions for truly exceptional, unrecoverable conditions.
/// </summary>
public class Result
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Gets the error associated with a failed result. <see cref="Error.None"/> when successful.</summary>
    public Error Error { get; }

    /// <summary>Creates a new <see cref="Result"/>. Enforces that success/failure state and error presence are consistent.</summary>
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot contain an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must contain an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Creates a successful result with no value.</summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>Creates a failed result with the given error.</summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Creates a successful result carrying a value.
    /// Throws <see cref="ArgumentNullException"/> if <paramref name="value"/> is null:
    /// a "successful" result with no value is a contradiction, so null is rejected
    /// rather than silently accepted. Use <see cref="Failure{TValue}"/> to represent
    /// an absent value.
    /// </summary>
    public static Result<TValue> Success<TValue>(TValue value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "A successful result cannot carry a null value.");
        }

        return new(value, true, Error.None);
    }

    /// <summary>Creates a failed result of the given value type with the specified error.</summary>
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

/// <summary>
/// Represents the outcome of an operation that, on success, produces a value of type <typeparamref name="TValue"/>.
/// </summary>
/// <typeparam name="TValue">The type of the value produced on success.</typeparam>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the value produced by a successful result.
    /// Throws <see cref="InvalidOperationException"/> if accessed on a failed result.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    /// <summary>Implicitly wraps a value into a successful <see cref="Result{TValue}"/>.</summary>
    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
