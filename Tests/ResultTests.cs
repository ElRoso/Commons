namespace ElRoso.Commons.Tests;

using ElRoso.Exceptions;
using ElRoso.Outcomes;
using FluentValidation.Results;
using System;
using System.Collections.Generic;

public class ResultTests
{
    [Fact]
    public void Success_HasNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_KeepsItsError()
    {
        var error = Error.Failure("unexpected", "Unexpected failure.");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void Value_OnFailure_ThrowsInvalidOperationException()
    {
        Result<string> result = Error.NotFound("missing", "Not found.");

        Assert.Throws<InvalidOperationException>(() => { _ = result.Value; });
    }

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccess()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ImplicitConversion_FromError_CreatesFailure()
    {
        var error = Error.Conflict("duplicate", "Already exists.");

        Result<int> result = error;

        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void ImplicitConversion_FromError_OnNonGenericResult_CreatesFailure()
    {
        var error = Error.Forbidden("denied", "Forbidden.");

        Result result = error;

        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void Match_SelectsSuccessBranch()
    {
        Result<int> result = 7;

        var output = result.Match(
            value => $"value:{value}",
            error => $"error:{error.Code}");

        Assert.Equal("value:7", output);
    }

    [Fact]
    public void Match_SelectsFailureBranch()
    {
        Result<int> result = Error.Validation("invalid", "Invalid.", new Dictionary<string, string[]>());

        var output = result.Match(
            value => $"value:{value}",
            error => $"error:{error.Code}");

        Assert.Equal("error:invalid", output);
    }

    [Fact]
    public void FromValidation_GroupsErrorsByPropertyLikeBadRequestException()
    {
        var validationResult = new ValidationResult(new List<ValidationFailure>
        {
            new ValidationFailure("Name", "Name is required."),
            new ValidationFailure("Name", "Name is too short."),
            new ValidationFailure("Age", "Age is invalid."),
        });

        var error = Error.FromValidation(validationResult);
        var expected = new BadRequestException(validationResult).Details;

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(expected.Count, error.Details.Count);
        foreach (var entry in expected)
        {
            Assert.Equal(entry.Value, error.Details[entry.Key]);
        }
    }

    [Fact]
    public void Factories_SetTheirErrorType()
    {
        Assert.Equal(ErrorType.Validation, Error.Validation("a", "a", new Dictionary<string, string[]>()).Type);
        Assert.Equal(ErrorType.NotFound, Error.NotFound("a", "a").Type);
        Assert.Equal(ErrorType.Conflict, Error.Conflict("a", "a").Type);
        Assert.Equal(ErrorType.Unauthorized, Error.Unauthorized("a", "a").Type);
        Assert.Equal(ErrorType.Forbidden, Error.Forbidden("a", "a").Type);
        Assert.Equal(ErrorType.Failure, Error.Failure("a", "a").Type);
        Assert.Empty(Error.Failure("a", "a").Details);
    }

    [Fact]
    public void GenericFailure_KeepsItsError()
    {
        var error = Error.Failure("x", "y");

        Result<int> result = Result<int>.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void GenericSuccess_KeepsItsValue()
    {
        Result<int> result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_WithNullError_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = Result.Failure(null!);
        });
    }

    [Fact]
    public void ImplicitConversion_FromNullError_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            Result _ = (Error)null!;
        });
    }

    [Fact]
    public void GenericFailure_WithNullError_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = Result<int>.Failure(null!);
        });
    }

    [Fact]
    public void GenericImplicitConversion_FromNullError_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            Result<int> _ = (Error)null!;
        });
    }
}
