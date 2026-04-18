using Feedback.Api.Data;
using Feedback.Api.Domain;
using Feedback.Api.Feedback.Mappers;
using Feedback.Api.Feedback.Requests;
using Feedback.Api.Feedback.Responses;
using Microsoft.EntityFrameworkCore;

namespace Feedback.Api.Services;

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
        return items.Select(FeedbackMapper.ToResponse).ToList();
    }

    public async Task<FeedbackDetailResponse> GetByIdAsync(int id)
    {
        var item = await db.Feedbacks
            .Include(f => f.Comments.OrderByDescending(c => c.CreatedAt))
            .FirstOrDefaultAsync(f => f.Id == id)
            ?? throw new KeyNotFoundException($"Feedback {id} not found.");

        return FeedbackMapper.ToDetailResponse(item);
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
        return FeedbackMapper.ToResponse(item);
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
        return FeedbackMapper.ToResponse(item);
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
        return FeedbackMapper.ToResponse(item);
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

        return FeedbackMapper.ToVoteResponse(vote);
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
        var item = await db.Feedbacks.FindAsync(feedbackId)
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

        return FeedbackMapper.ToCommentResponse(comment);
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
}
