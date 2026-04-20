namespace Feedback.Application.Feedback.Responses;

public record FeedbackResponse(
    int Id,
    string Title,
    string Description,
    string Type,
    string Status,
    string Priority,
    string AuthorName,
    string AuthorEmail,
    DateTime CreatedAt,
    int VoteCount);

public record FeedbackDetailResponse(
    int Id,
    string Title,
    string Description,
    string Type,
    string Status,
    string Priority,
    string AuthorName,
    string AuthorEmail,
    DateTime CreatedAt,
    int VoteCount,
    IReadOnlyList<CommentResponse> Comments);

public record CommentResponse(
    int Id,
    int FeedbackId,
    string AuthorName,
    string Content,
    DateTime CreatedAt,
    bool IsOfficial);

public record VoteResponse(
    int Id,
    int FeedbackId,
    string VoterEmail,
    DateTime CreatedAt);

public record FeedbackStatsResponse(
    IReadOnlyList<TypeStat> ByType,
    IReadOnlyList<StatusStat> ByStatus);

public record TypeStat(string Type, int Count);
public record StatusStat(string Status, int Count);
