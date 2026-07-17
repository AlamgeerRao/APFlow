using APFlow.Domain.Common;
using Xunit;

namespace APFlow.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ReturnsResultWithIsSuccessTrue_AndNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_ReturnsResultWithIsSuccessFalse_AndSuppliedError()
    {
        var error = new Error("Test.Code", "Test message");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void SuccessOfT_ReturnsResultWithValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void FailureOfT_AccessingValue_ThrowsInvalidOperationException()
    {
        var result = Result.Failure<int>(new Error("Test.Code", "Test message"));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromValue_ProducesSuccessResult()
    {
        Result<string> result = "hello";

        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void Success_WithNullReferenceValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Success<string>(null!));
    }

    [Fact]
    public void ImplicitConversion_FromNullReferenceValue_ThrowsArgumentNullException()
    {
        string? nullValue = null;

        Assert.Throws<ArgumentNullException>(() =>
        {
            Result<string> result = nullValue!;
        });
    }

    [Fact]
    public void Failure_WithErrorNone_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Failure(Error.None));
    }
}
