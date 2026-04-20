using Feedback.Application.Feedback.Requests;
using Feedback.Application.Feedback.Responses;
using Feedback.Domain;

namespace Feedback.Application.Abstractions;

public interface IFeedbackService
{
    Task<IReadOnlyList<FeedbackResponse>> GetAllAsync(FeedbackType? type, FeedbackStatus? status, bool sortByVotes);
    Task<FeedbackDetailResponse> GetByIdAsync(int id);
    Task<FeedbackResponse> CreateAsync(CreateFeedbackRequest request);
    Task<FeedbackResponse> UpdateAsync(int id, UpdateFeedbackRequest request);
    Task<FeedbackResponse> UpdateStatusAsync(int id, FeedbackStatus newStatus);
    Task<VoteResponse> AddVoteAsync(int feedbackId, AddVoteRequest request);
    Task RemoveVoteAsync(int feedbackId, string voterEmail);
    Task<CommentResponse> AddCommentAsync(int feedbackId, AddCommentRequest request);
    Task<FeedbackStatsResponse> GetStatsAsync();
}
