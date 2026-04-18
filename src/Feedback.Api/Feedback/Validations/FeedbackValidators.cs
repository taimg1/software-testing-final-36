using FluentValidation;
using Feedback.Api.Feedback.Requests;

namespace Feedback.Api.Feedback.Validations;

public class CreateFeedbackRequestValidator : AbstractValidator<CreateFeedbackRequest>
{
    public CreateFeedbackRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.AuthorName)
            .NotEmpty().WithMessage("Author name is required.")
            .MaximumLength(100).WithMessage("Author name must not exceed 100 characters.");

        RuleFor(x => x.AuthorEmail)
            .NotEmpty().WithMessage("Author email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid feedback type.");

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Invalid priority.");
    }
}

public class UpdateFeedbackRequestValidator : AbstractValidator<UpdateFeedbackRequest>
{
    public UpdateFeedbackRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid feedback type.");

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Invalid priority.");
    }
}

public class AddVoteRequestValidator : AbstractValidator<AddVoteRequest>
{
    public AddVoteRequestValidator()
    {
        RuleFor(x => x.VoterEmail)
            .NotEmpty().WithMessage("Voter email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}

public class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator()
    {
        RuleFor(x => x.AuthorName)
            .NotEmpty().WithMessage("Author name is required.")
            .MaximumLength(100).WithMessage("Author name must not exceed 100 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.")
            .MaximumLength(2000).WithMessage("Content must not exceed 2000 characters.");
    }
}
