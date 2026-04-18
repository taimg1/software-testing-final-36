using AutoFixture;
using Feedback.Api.Data;
using Feedback.Api.Domain;
using Feedback.Api.Feedback.Requests;
using Feedback.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Feedback.Api.Tests.Feedback;

public class FeedbackServiceTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly AppDbContext _db;
    private readonly FeedbackService _sut;
    private readonly IFixture _fixture;

    public FeedbackServiceTests()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero));

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _sut = new FeedbackService(_db, _timeProvider);

        _fixture = new Fixture();
    }

    public void Dispose() => _db.Dispose();

    // ── Status transition tests ────────────────────────────────────────────────

    [Theory]
    [InlineData(FeedbackStatus.Open, FeedbackStatus.UnderReview, true)]
    [InlineData(FeedbackStatus.Open, FeedbackStatus.Closed, true)]
    [InlineData(FeedbackStatus.UnderReview, FeedbackStatus.Planned, true)]
    [InlineData(FeedbackStatus.UnderReview, FeedbackStatus.Closed, true)]
    [InlineData(FeedbackStatus.Planned, FeedbackStatus.InProgress, true)]
    [InlineData(FeedbackStatus.Planned, FeedbackStatus.Closed, true)]
    [InlineData(FeedbackStatus.InProgress, FeedbackStatus.Done, true)]
    [InlineData(FeedbackStatus.InProgress, FeedbackStatus.Closed, true)]
    [InlineData(FeedbackStatus.Done, FeedbackStatus.Closed, true)]
    [InlineData(FeedbackStatus.Open, FeedbackStatus.Planned, false)]
    [InlineData(FeedbackStatus.Open, FeedbackStatus.InProgress, false)]
    [InlineData(FeedbackStatus.Open, FeedbackStatus.Done, false)]
    [InlineData(FeedbackStatus.UnderReview, FeedbackStatus.InProgress, false)]
    [InlineData(FeedbackStatus.UnderReview, FeedbackStatus.Done, false)]
    [InlineData(FeedbackStatus.Planned, FeedbackStatus.Open, false)]
    [InlineData(FeedbackStatus.Planned, FeedbackStatus.UnderReview, false)]
    [InlineData(FeedbackStatus.InProgress, FeedbackStatus.Open, false)]
    [InlineData(FeedbackStatus.Done, FeedbackStatus.Open, false)]
    [InlineData(FeedbackStatus.Closed, FeedbackStatus.Open, false)]
    [InlineData(FeedbackStatus.Closed, FeedbackStatus.Done, false)]
    public void IsValidTransition_ReturnsExpected(FeedbackStatus from, FeedbackStatus to, bool expected)
    {
        FeedbackService.IsValidTransition(from, to).ShouldBe(expected);
    }

    [Fact]
    public async Task UpdateStatus_ValidTransition_UpdatesStatusAsync()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        var result = await _sut.UpdateStatusAsync(created.Id, FeedbackStatus.UnderReview);
        result.Status.ShouldBe("UnderReview");
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_ThrowsInvalidOperationAsync()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync(created.Id, FeedbackStatus.Done));
        ex.Message.ShouldContain("Cannot transition");
    }

    [Fact]
    public async Task UpdateStatus_ClosedFeedback_CannotTransitionAsync()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.UpdateStatusAsync(created.Id, FeedbackStatus.Closed);
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.UpdateStatusAsync(created.Id, FeedbackStatus.Open));
    }

    [Fact]
    public async Task UpdateStatus_FullWorkflow_SucceedsAsync()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        var id = created.Id;

        await _sut.UpdateStatusAsync(id, FeedbackStatus.UnderReview);
        await _sut.UpdateStatusAsync(id, FeedbackStatus.Planned);
        await _sut.UpdateStatusAsync(id, FeedbackStatus.InProgress);
        var done = await _sut.UpdateStatusAsync(id, FeedbackStatus.Done);

        done.Status.ShouldBe("Done");
    }

    // ── Vote count logic tests ─────────────────────────────────────────────────

    [Fact]
    public async Task AddVote_FirstVote_IncrementsVoteCountAsync()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.AddVoteAsync(created.Id, new AddVoteRequest("voter@example.com"));

        var item = await _db.Feedbacks.FindAsync(created.Id);
        item!.VoteCount.ShouldBe(1);
    }

    [Fact]
    public async Task AddVote_MultipleVoters_CountsAllAsync()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());

        await _sut.AddVoteAsync(created.Id, new AddVoteRequest("voter1@example.com"));
        await _sut.AddVoteAsync(created.Id, new AddVoteRequest("voter2@example.com"));
        await _sut.AddVoteAsync(created.Id, new AddVoteRequest("voter3@example.com"));

        var item = await _db.Feedbacks.FindAsync(created.Id);
        item!.VoteCount.ShouldBe(3);
    }

    [Fact]
    public async Task RemoveVote_DecreasesVoteCountAsync()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.AddVoteAsync(created.Id, new AddVoteRequest("voter@example.com"));

        await _sut.RemoveVoteAsync(created.Id, "voter@example.com");

        var item = await _db.Feedbacks.FindAsync(created.Id);
        item!.VoteCount.ShouldBe(0);
    }

    [Fact]
    public async Task RemoveVote_NonExistent_ThrowsKeyNotFoundAsync()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.RemoveVoteAsync(created.Id, "nobody@example.com"));
    }

    [Fact]
    public async Task AddVote_DuplicateVote_ThrowsInvalidOperationAsync()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.AddVoteAsync(created.Id, new AddVoteRequest("voter@example.com"));

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.AddVoteAsync(created.Id, new AddVoteRequest("voter@example.com")));
        ex.Message.ShouldContain("already voted");
    }

    // ── Self-vote prevention tests ─────────────────────────────────────────────

    [Fact]
    public async Task AddVote_OwnFeedback_ThrowsInvalidOperationAsync()
    {
        var request = ValidCreateRequest() with { AuthorEmail = "author@example.com" };
        var created = await _sut.CreateAsync(request);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.AddVoteAsync(created.Id, new AddVoteRequest("author@example.com")));
        ex.Message.ShouldContain("own feedback");
    }

    [Fact]
    public async Task AddVote_OwnFeedbackCaseInsensitive_ThrowsInvalidOperationAsync()
    {
        var request = ValidCreateRequest() with { AuthorEmail = "Author@Example.com" };
        var created = await _sut.CreateAsync(request);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.AddVoteAsync(created.Id, new AddVoteRequest("author@example.com")));
    }

    [Fact]
    public async Task AddVote_DifferentEmail_SucceedsAsync()
    {
        var request = ValidCreateRequest() with { AuthorEmail = "author@example.com" };
        var created = await _sut.CreateAsync(request);

        var vote = await _sut.AddVoteAsync(created.Id, new AddVoteRequest("voter@example.com"));
        vote.ShouldNotBeNull();
    }

    // ── Create / Get / Update tests ────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_ReturnsCorrectDataAsync()
    {
        var result = await _sut.CreateAsync(ValidCreateRequest());

        result.Id.ShouldBeGreaterThan(0);
        result.Title.ShouldBe("Test Feedback");
        result.Status.ShouldBe("Open");
        result.VoteCount.ShouldBe(0);
        result.CreatedAt.ShouldBe(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetById_NonExistent_ThrowsKeyNotFoundAsync()
    {
        await Should.ThrowAsync<KeyNotFoundException>(() => _sut.GetByIdAsync(9999));
    }

    [Fact]
    public async Task GetById_IncludesCommentsAsync()
    {
        var created = await _sut.CreateAsync(ValidCreateRequest());
        await _sut.AddCommentAsync(created.Id, new AddCommentRequest("Admin", "Official reply", true));

        var detail = await _sut.GetByIdAsync(created.Id);
        detail.Comments.Count.ShouldBe(1);
        detail.Comments[0].IsOfficial.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAll_WithTypeFilter_ReturnsOnlyMatchingAsync()
    {
        await _sut.CreateAsync(ValidCreateRequest() with { Type = Domain.FeedbackType.Bug });
        await _sut.CreateAsync(ValidCreateRequest() with { Type = Domain.FeedbackType.Feature });
        await _sut.CreateAsync(ValidCreateRequest() with { Type = Domain.FeedbackType.Bug });

        var result = await _sut.GetAllAsync(Domain.FeedbackType.Bug, null, false);
        result.Count.ShouldBe(2);
        result.ShouldAllBe(f => f.Type == "Bug");
    }

    [Fact]
    public async Task GetAll_SortByVotes_OrdersCorrectlyAsync()
    {
        var f1 = await _sut.CreateAsync(ValidCreateRequest());
        var f2 = await _sut.CreateAsync(ValidCreateRequest());
        var f3 = await _sut.CreateAsync(ValidCreateRequest());

        await _sut.AddVoteAsync(f2.Id, new AddVoteRequest("v1@x.com"));
        await _sut.AddVoteAsync(f2.Id, new AddVoteRequest("v2@x.com"));
        await _sut.AddVoteAsync(f3.Id, new AddVoteRequest("v3@x.com"));

        var result = await _sut.GetAllAsync(null, null, sortByVotes: true);

        result[0].Id.ShouldBe(f2.Id);
        result[1].Id.ShouldBe(f3.Id);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static CreateFeedbackRequest ValidCreateRequest() => new(
        Title: "Test Feedback",
        Description: "Test description",
        Type: Domain.FeedbackType.Feature,
        Priority: Domain.FeedbackPriority.Medium,
        AuthorName: "John Doe",
        AuthorEmail: "john@example.com");
}
