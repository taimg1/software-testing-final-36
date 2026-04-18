using Feedback.Api.Domain;
using Feedback.Api.Feedback.Responses;

namespace Feedback.Api.Feedback.Mappers;

public static class FeedbackMapper
{
    public static FeedbackResponse ToResponse(FeedbackItem item) => new(
        item.Id,
        item.Title,
        item.Description,
        item.Type.ToString(),
        item.Status.ToString(),
        item.Priority.ToString(),
        item.AuthorName,
        item.AuthorEmail,
        item.CreatedAt,
        item.VoteCount);

    public static FeedbackDetailResponse ToDetailResponse(FeedbackItem item) => new(
        item.Id,
        item.Title,
        item.Description,
        item.Type.ToString(),
        item.Status.ToString(),
        item.Priority.ToString(),
        item.AuthorName,
        item.AuthorEmail,
        item.CreatedAt,
        item.VoteCount,
        item.Comments.Select(ToCommentResponse).ToList());

    public static CommentResponse ToCommentResponse(Comment comment) => new(
        comment.Id,
        comment.FeedbackId,
        comment.AuthorName,
        comment.Content,
        comment.CreatedAt,
        comment.IsOfficial);

    public static VoteResponse ToVoteResponse(Vote vote) => new(
        vote.Id,
        vote.FeedbackId,
        vote.VoterEmail,
        vote.CreatedAt);
}
