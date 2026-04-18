using Feedback.Api.Domain;

namespace Feedback.Api.Feedback.Requests;

public record CreateFeedbackRequest(
    string Title,
    string Description,
    FeedbackType Type,
    FeedbackPriority Priority,
    string AuthorName,
    string AuthorEmail);

public record UpdateFeedbackRequest(
    string Title,
    string Description,
    FeedbackType Type,
    FeedbackPriority Priority);

public record UpdateFeedbackStatusRequest(FeedbackStatus Status);

public record AddVoteRequest(string VoterEmail);

public record AddCommentRequest(
    string AuthorName,
    string Content,
    bool IsOfficial);
