using Feedback.Domain;
using Microsoft.EntityFrameworkCore;

namespace Feedback.Api.Tests.Database.Feedback;

public class StatusTransitionAuditTests : IClassFixture<FeedbackDatabaseFixture>
{
    private readonly FeedbackDatabaseFixture _fixture;

    public StatusTransitionAuditTests(FeedbackDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SeededData_ContainsAllStatusValuesAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var statuses = await db.Feedbacks
            .Select(f => f.Status)
            .Distinct()
            .ToListAsync();

        // 10,000 items spread across 6 statuses — all should appear
        statuses.ShouldContain(FeedbackStatus.Open);
        statuses.ShouldContain(FeedbackStatus.UnderReview);
        statuses.ShouldContain(FeedbackStatus.Planned);
        statuses.ShouldContain(FeedbackStatus.InProgress);
        statuses.ShouldContain(FeedbackStatus.Done);
        statuses.ShouldContain(FeedbackStatus.Closed);
    }

    [Fact]
    public async Task StatusUpdate_PersistsCorrectlyAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var feedback = await db.Feedbacks.FirstAsync(f => f.Status == FeedbackStatus.Open);

        feedback.Status = FeedbackStatus.UnderReview;
        await db.SaveChangesAsync();

        await using var db2 = _fixture.CreateDbContext();
        var updated = await db2.Feedbacks.FindAsync(feedback.Id);
        updated!.Status.ShouldBe(FeedbackStatus.UnderReview);
    }

    [Fact]
    public async Task MultipleStatusUpdates_PersistAllChangesAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var feedbacks = await db.Feedbacks
            .Where(f => f.Status == FeedbackStatus.Open)
            .Take(5)
            .ToListAsync();

        feedbacks.Count.ShouldBe(5);

        foreach (var f in feedbacks)
            f.Status = FeedbackStatus.Closed;

        await db.SaveChangesAsync();

        await using var db2 = _fixture.CreateDbContext();
        var ids = feedbacks.Select(f => f.Id).ToList();
        var closedCount = await db2.Feedbacks
            .Where(f => ids.Contains(f.Id) && f.Status == FeedbackStatus.Closed)
            .CountAsync();

        closedCount.ShouldBe(5);
    }

    [Fact]
    public async Task FeedbackWithComments_IncludesOfficialFlagAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var officialComments = await db.Comments
            .Where(c => c.IsOfficial)
            .CountAsync();

        officialComments.ShouldBeGreaterThan(0);
    }
}
