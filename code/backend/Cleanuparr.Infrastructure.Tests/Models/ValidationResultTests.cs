using Cleanuparr.Infrastructure.Models;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Models;

public class ValidationResultTests
{
    [Fact]
    public void Success_ReturnsValidResult()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Success_HasEmptyErrorMessage()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        result.ErrorMessage.ShouldBe(string.Empty);
    }

    [Fact]
    public void Success_HasEmptyDetails()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        result.Details.ShouldBeEmpty();
    }

    [Fact]
    public void Failure_ReturnsInvalidResult()
    {
        // Act
        var result = ValidationResult.Failure("Error occurred");

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Failure_ContainsErrorMessage()
    {
        // Arrange
        const string errorMessage = "Validation failed";

        // Act
        var result = ValidationResult.Failure(errorMessage);

        // Assert
        result.ErrorMessage.ShouldBe(errorMessage);
    }

    [Fact]
    public void Failure_WithDetails_ContainsAllDetails()
    {
        // Arrange
        const string errorMessage = "Multiple errors";
        var details = new List<string> { "Error 1", "Error 2", "Error 3" };

        // Act
        var result = ValidationResult.Failure(errorMessage, details);

        // Assert
        result.Details.Count.ShouldBe(3);
        result.Details.ShouldContain("Error 1");
        result.Details.ShouldContain("Error 2");
        result.Details.ShouldContain("Error 3");
    }

    [Fact]
    public void Failure_WithoutDetails_HasEmptyDetailsList()
    {
        // Act
        var result = ValidationResult.Failure("Error");

        // Assert
        result.Details.ShouldNotBeNull();
        result.Details.ShouldBeEmpty();
    }

    [Fact]
    public void Failure_WithNullDetails_HasEmptyDetailsList()
    {
        // Act
        var result = ValidationResult.Failure("Error", null);

        // Assert
        result.Details.ShouldNotBeNull();
        result.Details.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultConstructor_IsValidIsFalse()
    {
        // Act
        var result = new ValidationResult();

        // Assert
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void DefaultConstructor_ErrorMessageIsEmpty()
    {
        // Act
        var result = new ValidationResult();

        // Assert
        result.ErrorMessage.ShouldBe(string.Empty);
    }

    [Fact]
    public void DefaultConstructor_DetailsIsEmptyList()
    {
        // Act
        var result = new ValidationResult();

        // Assert
        result.Details.ShouldNotBeNull();
        result.Details.ShouldBeEmpty();
    }

    [Fact]
    public void Properties_CanBeSetDirectly()
    {
        // Arrange
        var result = new ValidationResult();

        // Act
        result.IsValid = true;
        result.ErrorMessage = "Test error";
        result.Details = new List<string> { "Detail 1" };

        // Assert
        result.IsValid.ShouldBeTrue();
        result.ErrorMessage.ShouldBe("Test error");
        result.Details.ShouldContain("Detail 1");
    }
}
