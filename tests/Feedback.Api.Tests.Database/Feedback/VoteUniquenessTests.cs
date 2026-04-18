using Feedback.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Feedback.Api.Tests.Database.Feedback;

public class VoteUniquenessTests : IClassFixture<FeedbackDatabaseFixture>
{
    private readonly FeedbackDatabaseFixture _fixture;

    public VoteUniquenessTests(FeedbackDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Vote_UniqueConstraint_PreventsDuplicatesAtDatabaseLevelAsync()
    {
        await using var db = _fixture.CreateDbContext();

        // Get a feedback that has no votes yet
        var feedback = await db.Feedbacks
            .FirstAsync(f => f.Status == FeedbackStatus.Open);

        const string voterEmail = "unique-test@example.com";

        var vote1 = new Vote
        {
            FeedbackId = feedback.Id,
            VoterEmail = voterEmail,
            CreatedAt = DateTime.UtcNow,
        };
        db.Votes.Add(vote1);
        await db.SaveChangesAsync();

        // Try adding duplicate — should throw at DB level
        await using var db2 = _fixture.CreateDbContext();
        var vote2 = new Vote
        {
            FeedbackId = feedback.Id,
            VoterEmail = voterEmail,
            CreatedAt = DateTime.UtcNow,
        };
        db2.Votes.Add(vote2);

        var ex = await Should.ThrowAsync<Exception>(() => db2.SaveChangesAsync());
        ex.ShouldNotBeNull();
    }

    [Fact]
    public async Task Vote_SameEmailDifferentFeedback_IsAllowedAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var feedbacks = await db.Feedbacks
            .Where(f => f.Status == FeedbackStatus.Open)
            .Take(2)
            .ToListAsync();

        feedbacks.Count.ShouldBe(2);

        const string voterEmail = "cross-feedback@example.com";

        db.Votes.AddRange(
            new Vote { FeedbackId = feedbacks[0].Id, VoterEmail = voterEmail, CreatedAt = DateTime.UtcNow },
            new Vote { FeedbackId = feedbacks[1].Id, VoterEmail = voterEmail, CreatedAt = DateTime.UtcNow }
        );

        // Should not throw
        await db.SaveChangesAsync();

        var count = await db.Votes.CountAsync(v => v.VoterEmail == voterEmail);
        count.ShouldBe(2);
    }

    [Fact]
    public async Task Vote_DifferentEmailSameFeedback_IsAllowedAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var feedback = await db.Feedbacks.FirstAsync();

        var email1 = $"voter-a-{Guid.NewGuid():N}@example.com";
        var email2 = $"voter-b-{Guid.NewGuid():N}@example.com";

        db.Votes.AddRange(
            new Vote { FeedbackId = feedback.Id, VoterEmail = email1, CreatedAt = DateTime.UtcNow },
            new Vote { FeedbackId = feedback.Id, VoterEmail = email2, CreatedAt = DateTime.UtcNow }
        );

        await db.SaveChangesAsync();

        var count = await db.Votes
            .CountAsync(v => v.FeedbackId == feedback.Id &&
                             (v.VoterEmail == email1 || v.VoterEmail == email2));
        count.ShouldBe(2);
    }
}
