using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using Feedback.Api.Data;
using Feedback.Api.Domain;
using Feedback.Api.Feedback.Requests;
using Feedback.Api.Feedback.Responses;
using Microsoft.Extensions.DependencyInjection;

namespace Feedback.Api.Tests.Integration.Feedback;

public class FeedbackControllerTests : IClassFixture<FeedbackApiFactory>, IAsyncLifetime
{
    private readonly FeedbackApiFactory _factory;
    private readonly HttpClient _client;
    private readonly IFixture _fixture;

    public FeedbackControllerTests(FeedbackApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _fixture = new Fixture();
    }

    public async ValueTask InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Comments.RemoveRange(db.Comments);
        db.Votes.RemoveRange(db.Votes);
        db.Feedbacks.RemoveRange(db.Feedbacks);
        await db.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ── GET /api/feedback ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_NoFeedback_ReturnsEmptyListAsync()
    {
        var response = await _client.GetAsync("/api/feedback");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<FeedbackResponse>>();
        items.ShouldNotBeNull();
        items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAll_AfterCreating_ReturnsAllAsync()
    {
        await _client.PostAsJsonAsync("/api/feedback", ValidCreateRequest());
        await _client.PostAsJsonAsync("/api/feedback", ValidCreateRequest("another@test.com"));

        var response = await _client.GetAsync("/api/feedback");
        var items = await response.Content.ReadFromJsonAsync<List<FeedbackResponse>>();
        items!.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAll_FilterByType_ReturnsMatchingOnlyAsync()
    {
        await _client.PostAsJsonAsync("/api/feedback",
            ValidCreateRequest() with { Type = FeedbackType.Bug });
        await _client.PostAsJsonAsync("/api/feedback",
            ValidCreateRequest("a2@test.com") with { Type = FeedbackType.Feature });

        var response = await _client.GetAsync("/api/feedback?type=Bug");
        var items = await response.Content.ReadFromJsonAsync<List<FeedbackResponse>>();
        items!.Count.ShouldBe(1);
        items[0].Type.ShouldBe("Bug");
    }

    [Fact]
    public async Task GetAll_FilterByStatus_ReturnsMatchingOnlyAsync()
    {
        var created = await CreateFeedbackAsync();
        await _client.PatchAsJsonAsync($"/api/feedback/{created.Id}/status",
            new UpdateFeedbackStatusRequest(FeedbackStatus.UnderReview));

        var openResponse = await _client.GetAsync("/api/feedback?status=Open");
        var openItems = await openResponse.Content.ReadFromJsonAsync<List<FeedbackResponse>>();
        openItems!.ShouldBeEmpty();

        var reviewResponse = await _client.GetAsync("/api/feedback?status=UnderReview");
        var reviewItems = await reviewResponse.Content.ReadFromJsonAsync<List<FeedbackResponse>>();
        reviewItems!.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetAll_SortByVotes_OrdersCorrectlyAsync()
    {
        var f1 = await CreateFeedbackAsync("a1@test.com");
        var f2 = await CreateFeedbackAsync("a2@test.com");

        // f2 gets 2 votes; f1 gets 0
        await _client.PostAsJsonAsync($"/api/feedback/{f2.Id}/vote",
            new AddVoteRequest("v1@test.com"));
        await _client.PostAsJsonAsync($"/api/feedback/{f2.Id}/vote",
            new AddVoteRequest("v2@test.com"));

        var response = await _client.GetAsync("/api/feedback?sortByVotes=true");
        var items = await response.Content.ReadFromJsonAsync<List<FeedbackResponse>>();

        items![0].Id.ShouldBe(f2.Id);
        items[1].Id.ShouldBe(f1.Id);
    }

    // ── POST /api/feedback ─────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidRequest_Returns201Async()
    {
        var response = await _client.PostAsJsonAsync("/api/feedback", ValidCreateRequest());
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var item = await response.Content.ReadFromJsonAsync<FeedbackResponse>();
        item.ShouldNotBeNull();
        item.Status.ShouldBe("Open");
        item.VoteCount.ShouldBe(0);
        response.Headers.Location.ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_InvalidRequest_Returns400Async()
    {
        var request = ValidCreateRequest() with { Title = "", AuthorEmail = "bad" };
        var response = await _client.PostAsJsonAsync("/api/feedback", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── GET /api/feedback/{id} ─────────────────────────────────────────────────

    [Fact]
    public async Task GetById_Existing_ReturnsWithCommentsAsync()
    {
        var created = await CreateFeedbackAsync();
        await _client.PostAsJsonAsync($"/api/feedback/{created.Id}/comments",
            new AddCommentRequest("Admin", "Official response", true));

        var response = await _client.GetAsync($"/api/feedback/{created.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<FeedbackDetailResponse>();
        detail.ShouldNotBeNull();
        detail.Comments.Count.ShouldBe(1);
        detail.Comments[0].IsOfficial.ShouldBeTrue();
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404Async()
    {
        var response = await _client.GetAsync("/api/feedback/99999");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── PUT /api/feedback/{id} ─────────────────────────────────────────────────

    [Fact]
    public async Task Update_Valid_Returns200Async()
    {
        var created = await CreateFeedbackAsync();
        var updateRequest = new UpdateFeedbackRequest(
            "Updated Title", "Updated description",
            FeedbackType.Bug, FeedbackPriority.High);

        var response = await _client.PutAsJsonAsync($"/api/feedback/{created.Id}", updateRequest);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var item = await response.Content.ReadFromJsonAsync<FeedbackResponse>();
        item!.Title.ShouldBe("Updated Title");
        item.Type.ShouldBe("Bug");
    }

    [Fact]
    public async Task Update_NonExistent_Returns404Async()
    {
        var response = await _client.PutAsJsonAsync("/api/feedback/99999",
            new UpdateFeedbackRequest("T", "D", FeedbackType.Bug, FeedbackPriority.Low));
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── PATCH /api/feedback/{id}/status ───────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_ValidTransition_Returns200Async()
    {
        var created = await CreateFeedbackAsync();
        var response = await _client.PatchAsJsonAsync(
            $"/api/feedback/{created.Id}/status",
            new UpdateFeedbackStatusRequest(FeedbackStatus.UnderReview));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var item = await response.Content.ReadFromJsonAsync<FeedbackResponse>();
        item!.Status.ShouldBe("UnderReview");
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_Returns409Async()
    {
        var created = await CreateFeedbackAsync();
        var response = await _client.PatchAsJsonAsync(
            $"/api/feedback/{created.Id}/status",
            new UpdateFeedbackStatusRequest(FeedbackStatus.Done));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateStatus_FullWorkflow_SucceedsAsync()
    {
        var created = await CreateFeedbackAsync();
        var id = created.Id;

        await PatchStatusAsync(id, FeedbackStatus.UnderReview);
        await PatchStatusAsync(id, FeedbackStatus.Planned);
        await PatchStatusAsync(id, FeedbackStatus.InProgress);
        var finalResponse = await PatchStatusAsync(id, FeedbackStatus.Done);

        finalResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var item = await finalResponse.Content.ReadFromJsonAsync<FeedbackResponse>();
        item!.Status.ShouldBe("Done");
    }

    // ── POST /api/feedback/{id}/vote ───────────────────────────────────────────

    [Fact]
    public async Task Vote_Valid_Returns201AndIncrementsCountAsync()
    {
        var created = await CreateFeedbackAsync("author@test.com");
        var response = await _client.PostAsJsonAsync(
            $"/api/feedback/{created.Id}/vote",
            new AddVoteRequest("voter@test.com"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var afterVote = await _client.GetAsync($"/api/feedback/{created.Id}");
        var detail = await afterVote.Content.ReadFromJsonAsync<FeedbackDetailResponse>();
        detail!.VoteCount.ShouldBe(1);
    }

    [Fact]
    public async Task Vote_OwnFeedback_Returns409Async()
    {
        var created = await CreateFeedbackAsync("author@test.com");
        var response = await _client.PostAsJsonAsync(
            $"/api/feedback/{created.Id}/vote",
            new AddVoteRequest("author@test.com"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Vote_DuplicateVote_Returns409Async()
    {
        var created = await CreateFeedbackAsync("author@test.com");
        await _client.PostAsJsonAsync($"/api/feedback/{created.Id}/vote",
            new AddVoteRequest("voter@test.com"));

        var response = await _client.PostAsJsonAsync($"/api/feedback/{created.Id}/vote",
            new AddVoteRequest("voter@test.com"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ── DELETE /api/feedback/{id}/vote ─────────────────────────────────────────

    [Fact]
    public async Task RemoveVote_Existing_Returns204AndDecrementsCountAsync()
    {
        var created = await CreateFeedbackAsync("author@test.com");
        await _client.PostAsJsonAsync($"/api/feedback/{created.Id}/vote",
            new AddVoteRequest("voter@test.com"));

        var response = await _client.DeleteAsync(
            $"/api/feedback/{created.Id}/vote?voterEmail=voter@test.com");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterRemove = await _client.GetAsync($"/api/feedback/{created.Id}");
        var detail = await afterRemove.Content.ReadFromJsonAsync<FeedbackDetailResponse>();
        detail!.VoteCount.ShouldBe(0);
    }

    [Fact]
    public async Task RemoveVote_NonExistent_Returns404Async()
    {
        var created = await CreateFeedbackAsync();
        var response = await _client.DeleteAsync(
            $"/api/feedback/{created.Id}/vote?voterEmail=nobody@test.com");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── POST /api/feedback/{id}/comments ──────────────────────────────────────

    [Fact]
    public async Task AddComment_Valid_Returns201Async()
    {
        var created = await CreateFeedbackAsync();
        var response = await _client.PostAsJsonAsync(
            $"/api/feedback/{created.Id}/comments",
            new AddCommentRequest("Support Team", "We are looking into this.", true));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var comment = await response.Content.ReadFromJsonAsync<CommentResponse>();
        comment.ShouldNotBeNull();
        comment.IsOfficial.ShouldBeTrue();
    }

    [Fact]
    public async Task AddComment_Invalid_Returns400Async()
    {
        var created = await CreateFeedbackAsync();
        var response = await _client.PostAsJsonAsync(
            $"/api/feedback/{created.Id}/comments",
            new AddCommentRequest("", "", false));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── GET /api/feedback/stats ────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_ReturnsByTypeAndStatusAsync()
    {
        await _client.PostAsJsonAsync("/api/feedback",
            ValidCreateRequest() with { Type = FeedbackType.Bug });
        await _client.PostAsJsonAsync("/api/feedback",
            ValidCreateRequest("a2@test.com") with { Type = FeedbackType.Feature });
        await _client.PostAsJsonAsync("/api/feedback",
            ValidCreateRequest("a3@test.com") with { Type = FeedbackType.Bug });

        var response = await _client.GetAsync("/api/feedback/stats");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stats = await response.Content.ReadFromJsonAsync<FeedbackStatsResponse>();
        stats.ShouldNotBeNull();
        stats.ByType.Count.ShouldBeGreaterThan(0);
        stats.ByStatus.Count.ShouldBeGreaterThan(0);

        var bugStat = stats.ByType.FirstOrDefault(s => s.Type == "Bug");
        bugStat.ShouldNotBeNull();
        bugStat.Count.ShouldBe(2);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<FeedbackResponse> CreateFeedbackAsync(string authorEmail = "default@test.com")
    {
        var response = await _client.PostAsJsonAsync("/api/feedback",
            ValidCreateRequest(authorEmail));
        return (await response.Content.ReadFromJsonAsync<FeedbackResponse>())!;
    }

    private async Task<HttpResponseMessage> PatchStatusAsync(int id, FeedbackStatus status) =>
        await _client.PatchAsJsonAsync($"/api/feedback/{id}/status",
            new UpdateFeedbackStatusRequest(status));

    private static CreateFeedbackRequest ValidCreateRequest(string authorEmail = "author@test.com") => new(
        Title: "Test Feedback Title",
        Description: "A detailed description of the feedback",
        Type: FeedbackType.Feature,
        Priority: FeedbackPriority.Medium,
        AuthorName: "Test Author",
        AuthorEmail: authorEmail);
}
