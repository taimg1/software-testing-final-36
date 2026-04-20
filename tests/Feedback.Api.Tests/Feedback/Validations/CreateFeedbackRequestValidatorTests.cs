using Feedback.Application.Feedback.Requests;
using Feedback.Application.Feedback.Validators;
using Feedback.Domain;
using FluentValidation.TestHelper;

namespace Feedback.Api.Tests.Feedback.Validations;

public class CreateFeedbackRequestValidatorTests
{
    private static readonly CreateFeedbackRequestValidator Validator = new();

    [Fact]
    public void ValidRequest_PassesAllRules()
    {
        var result = Validator.TestValidate(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Title_Empty_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { Title = "" });
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_TooLong_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { Title = new string('A', 201) });
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Title_AtMaxLength_NoError()
    {
        var result = Validator.TestValidate(ValidRequest() with { Title = new string('A', 200) });
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Description_Empty_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { Description = "" });
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Description_TooLong_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { Description = new string('A', 2001) });
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void AuthorEmail_Invalid_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { AuthorEmail = "not-an-email" });
        result.ShouldHaveValidationErrorFor(x => x.AuthorEmail);
    }

    [Fact]
    public void AuthorName_Empty_HasError()
    {
        var result = Validator.TestValidate(ValidRequest() with { AuthorName = "" });
        result.ShouldHaveValidationErrorFor(x => x.AuthorName);
    }

    [Theory]
    [InlineData(FeedbackType.Bug)]
    [InlineData(FeedbackType.Feature)]
    [InlineData(FeedbackType.Improvement)]
    public void Type_ValidEnum_NoError(FeedbackType type)
    {
        var result = Validator.TestValidate(ValidRequest() with { Type = type });
        result.ShouldNotHaveValidationErrorFor(x => x.Type);
    }

    [Theory]
    [InlineData(FeedbackPriority.Low)]
    [InlineData(FeedbackPriority.Medium)]
    [InlineData(FeedbackPriority.High)]
    public void Priority_ValidEnum_NoError(FeedbackPriority priority)
    {
        var result = Validator.TestValidate(ValidRequest() with { Priority = priority });
        result.ShouldNotHaveValidationErrorFor(x => x.Priority);
    }

    private static CreateFeedbackRequest ValidRequest() => new(
        Title: "Valid title",
        Description: "Valid description",
        Type: FeedbackType.Feature,
        Priority: FeedbackPriority.Medium,
        AuthorName: "John Doe",
        AuthorEmail: "john@example.com");
}
