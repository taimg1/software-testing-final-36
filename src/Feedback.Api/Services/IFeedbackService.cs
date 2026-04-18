using Feedback.Api.Domain;
using Feedback.Api.Feedback.Requests;
using Feedback.Api.Feedback.Responses;

namespace Feedback.Api.Services;

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
