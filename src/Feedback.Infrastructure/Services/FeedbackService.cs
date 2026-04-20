using Feedback.Application.Abstractions;
using Feedback.Application.Feedback.Requests;
using Feedback.Application.Feedback.Responses;
using Feedback.Domain;
using Feedback.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Feedback.Infrastructure.Services;

public class FeedbackService(AppDbContext db, TimeProvider timeProvider) : IFeedbackService
{
    // Valid status transitions: Open -> UnderReview -> Planned -> InProgress -> Done
    // Closed can be reached from any status
    private static readonly Dictionary<FeedbackStatus, FeedbackStatus[]> AllowedTransitions = new()
    {
        [FeedbackStatus.Open] = [FeedbackStatus.UnderReview, FeedbackStatus.Closed],
        [FeedbackStatus.UnderReview] = [FeedbackStatus.Planned, FeedbackStatus.Closed],
        [FeedbackStatus.Planned] = [FeedbackStatus.InProgress, FeedbackStatus.Closed],
        [FeedbackStatus.InProgress] = [FeedbackStatus.Done, FeedbackStatus.Closed],
        [FeedbackStatus.Done] = [FeedbackStatus.Closed],
        [FeedbackStatus.Closed] = [],
    };

    public static bool IsValidTransition(FeedbackStatus from, FeedbackStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    public async Task<IReadOnlyList<FeedbackResponse>> GetAllAsync(
        FeedbackType? type, FeedbackStatus? status, bool sortByVotes)
    {
        var query = db.Feedbacks.AsQueryable();

        if (type.HasValue)
            query = query.Where(f => f.Type == type.Value);

        if (status.HasValue)
            query = query.Where(f => f.Status == status.Value);

        query = sortByVotes
            ? query.OrderByDescending(f => f.VoteCount)
            : query.OrderByDescending(f => f.CreatedAt);

        var items = await query.ToListAsync();
        return items.Select(ToResponse).ToList();
    }

    public async Task<FeedbackDetailResponse> GetByIdAsync(int id)
    {
        var item = await db.Feedbacks
            .Include(f => f.Comments.OrderByDescending(c => c.CreatedAt))
            .FirstOrDefaultAsync(f => f.Id == id)
            ?? throw new KeyNotFoundException($"Feedback {id} not found.");

        return ToDetailResponse(item);
    }

    public async Task<FeedbackResponse> CreateAsync(CreateFeedbackRequest request)
    {
        var item = new FeedbackItem
        {
            Title = request.Title,
            Description = request.Description,
            Type = request.Type,
            Priority = request.Priority,
            Status = FeedbackStatus.Open,
            AuthorName = request.AuthorName,
            AuthorEmail = request.AuthorEmail,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            VoteCount = 0,
        };

        db.Feedbacks.Add(item);
        await db.SaveChangesAsync();
        return ToResponse(item);
    }

    public async Task<FeedbackResponse> UpdateAsync(int id, UpdateFeedbackRequest request)
    {
        var item = await db.Feedbacks.FindAsync(id)
            ?? throw new KeyNotFoundException($"Feedback {id} not found.");

        item.Title = request.Title;
        item.Description = request.Description;
        item.Type = request.Type;
        item.Priority = request.Priority;

        await db.SaveChangesAsync();
        return ToResponse(item);
    }

    public async Task<FeedbackResponse> UpdateStatusAsync(int id, FeedbackStatus newStatus)
    {
        var item = await db.Feedbacks.FindAsync(id)
            ?? throw new KeyNotFoundException($"Feedback {id} not found.");

        if (!IsValidTransition(item.Status, newStatus))
            throw new InvalidOperationException(
                $"Cannot transition from {item.Status} to {newStatus}.");

        item.Status = newStatus;
        await db.SaveChangesAsync();
        return ToResponse(item);
    }

    public async Task<VoteResponse> AddVoteAsync(int feedbackId, AddVoteRequest request)
    {
        var item = await db.Feedbacks.FindAsync(feedbackId)
            ?? throw new KeyNotFoundException($"Feedback {feedbackId} not found.");

        if (item.AuthorEmail.Equals(request.VoterEmail, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot vote for your own feedback.");

        var existing = await db.Votes.FirstOrDefaultAsync(v =>
            v.FeedbackId == feedbackId &&
            v.VoterEmail.ToLower() == request.VoterEmail.ToLower());

        if (existing != null)
            throw new InvalidOperationException("You have already voted for this feedback.");

        var vote = new Vote
        {
            FeedbackId = feedbackId,
            VoterEmail = request.VoterEmail,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        db.Votes.Add(vote);
        item.VoteCount++;
        await db.SaveChangesAsync();

        return ToVoteResponse(vote);
    }

    public async Task RemoveVoteAsync(int feedbackId, string voterEmail)
    {
        var item = await db.Feedbacks.FindAsync(feedbackId)
            ?? throw new KeyNotFoundException($"Feedback {feedbackId} not found.");

        var vote = await db.Votes.FirstOrDefaultAsync(v =>
            v.FeedbackId == feedbackId &&
            v.VoterEmail.ToLower() == voterEmail.ToLower())
            ?? throw new KeyNotFoundException("Vote not found.");

        db.Votes.Remove(vote);
        item.VoteCount = Math.Max(0, item.VoteCount - 1);
        await db.SaveChangesAsync();
    }

    public async Task<CommentResponse> AddCommentAsync(int feedbackId, AddCommentRequest request)
    {
        _ = await db.Feedbacks.FindAsync(feedbackId)
            ?? throw new KeyNotFoundException($"Feedback {feedbackId} not found.");

        var comment = new Comment
        {
            FeedbackId = feedbackId,
            AuthorName = request.AuthorName,
            Content = request.Content,
            IsOfficial = request.IsOfficial,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };

        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        return ToCommentResponse(comment);
    }

    public async Task<FeedbackStatsResponse> GetStatsAsync()
    {
        var byType = await db.Feedbacks
            .GroupBy(f => f.Type)
            .Select(g => new TypeStat(g.Key.ToString(), g.Count()))
            .ToListAsync();

        var byStatus = await db.Feedbacks
            .GroupBy(f => f.Status)
            .Select(g => new StatusStat(g.Key.ToString(), g.Count()))
            .ToListAsync();

        return new FeedbackStatsResponse(byType, byStatus);
    }

    private static FeedbackResponse ToResponse(FeedbackItem item) => new(
        item.Id, item.Title, item.Description,
        item.Type.ToString(), item.Status.ToString(), item.Priority.ToString(),
        item.AuthorName, item.AuthorEmail, item.CreatedAt, item.VoteCount);

    private static FeedbackDetailResponse ToDetailResponse(FeedbackItem item) => new(
        item.Id, item.Title, item.Description,
        item.Type.ToString(), item.Status.ToString(), item.Priority.ToString(),
        item.AuthorName, item.AuthorEmail, item.CreatedAt, item.VoteCount,
        item.Comments.Select(ToCommentResponse).ToList());

    private static CommentResponse ToCommentResponse(Comment comment) => new(
        comment.Id, comment.FeedbackId, comment.AuthorName,
        comment.Content, comment.CreatedAt, comment.IsOfficial);

    private static VoteResponse ToVoteResponse(Vote vote) => new(
        vote.Id, vote.FeedbackId, vote.VoterEmail, vote.CreatedAt);
}
