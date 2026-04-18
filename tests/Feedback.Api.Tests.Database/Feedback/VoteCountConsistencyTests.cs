using Feedback.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Feedback.Api.Tests.Database.Feedback;

public class VoteCountConsistencyTests : IClassFixture<FeedbackDatabaseFixture>
{
    private readonly FeedbackDatabaseFixture _fixture;

    public VoteCountConsistencyTests(FeedbackDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task VoteCount_MatchesActualVotesInDatabaseAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var feedbacks = await db.Feedbacks
            .Include(f => f.Votes)
            .ToListAsync();

        foreach (var feedback in feedbacks)
        {
            feedback.VoteCount.ShouldBe(feedback.Votes.Count,
                $"FeedbackId={feedback.Id} VoteCount mismatch");
        }
    }

    [Fact]
    public async Task VoteCount_AfterManualVoteInsert_CanBeRecalculatedAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var feedback = await db.Feedbacks.FirstAsync(f => f.VoteCount == 0);
        var initialCount = feedback.VoteCount;

        var email = $"manual-{Guid.NewGuid():N}@example.com";
        db.Votes.Add(new Vote
        {
            FeedbackId = feedback.Id,
            VoterEmail = email,
            CreatedAt = DateTime.UtcNow,
        });
        feedback.VoteCount++;
        await db.SaveChangesAsync();

        await using var db2 = _fixture.CreateDbContext();
        var actual = await db2.Feedbacks
            .Include(f => f.Votes)
            .FirstAsync(f => f.Id == feedback.Id);

        actual.VoteCount.ShouldBe(initialCount + 1);
        actual.Votes.Count.ShouldBe(initialCount + 1);
    }

    [Fact]
    public async Task SeededData_HasPositiveVoteCountsAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var totalVotes = await db.Votes.CountAsync();
        totalVotes.ShouldBeGreaterThan(0);

        var feedbacksWithVotes = await db.Feedbacks
            .Where(f => f.VoteCount > 0)
            .CountAsync();
        feedbacksWithVotes.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task SeededData_HasExpectedCountsAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var feedbackCount = await db.Feedbacks.CountAsync();
        feedbackCount.ShouldBeGreaterThanOrEqualTo(200);

        var voteCount = await db.Votes.CountAsync();
        voteCount.ShouldBeGreaterThan(0);

        var commentCount = await db.Comments.CountAsync();
        commentCount.ShouldBeGreaterThan(0);
    }
}
