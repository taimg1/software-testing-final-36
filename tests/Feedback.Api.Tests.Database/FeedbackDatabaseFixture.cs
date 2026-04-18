using Bogus;
using Feedback.Api.Data;
using Feedback.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Feedback.Api.Tests.Database;

public class FeedbackDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        await SeedDataAsync(db);
    }

    private static async Task SeedDataAsync(AppDbContext db)
    {
        var faker = new Faker();
        var random = new Random(42);

        var feedbacks = new List<FeedbackItem>();
        for (var i = 0; i < 200; i++)
        {
            feedbacks.Add(new FeedbackItem
            {
                Title = faker.Lorem.Sentence(5),
                Description = faker.Lorem.Paragraph(),
                Type = (FeedbackType)(i % 3),
                Status = (FeedbackStatus)(i % 6),
                Priority = (FeedbackPriority)(i % 3),
                AuthorName = faker.Name.FullName(),
                AuthorEmail = faker.Internet.Email(),
                CreatedAt = faker.Date.Past(2).ToUniversalTime(),
                VoteCount = 0,
            });
        }

        db.Feedbacks.AddRange(feedbacks);
        await db.SaveChangesAsync();

        // Add votes (ensuring uniqueness: one per voter per feedback)
        var votes = new List<Vote>();
        var voteSet = new HashSet<(int, string)>();

        foreach (var feedback in feedbacks.Take(100))
        {
            var voterCount = random.Next(1, 10);
            for (var v = 0; v < voterCount; v++)
            {
                var email = faker.Internet.Email();
                if (voteSet.Add((feedback.Id, email.ToLower())) &&
                    !email.Equals(feedback.AuthorEmail, StringComparison.OrdinalIgnoreCase))
                {
                    votes.Add(new Vote
                    {
                        FeedbackId = feedback.Id,
                        VoterEmail = email,
                        CreatedAt = faker.Date.Recent(30).ToUniversalTime(),
                    });
                    feedback.VoteCount++;
                }
            }
        }

        db.Votes.AddRange(votes);

        // Add comments (50 per 200 feedbacks ~= realistic ratio)
        var comments = new List<Comment>();
        foreach (var feedback in feedbacks.Take(50))
        {
            var commentCount = random.Next(1, 6);
            for (var c = 0; c < commentCount; c++)
            {
                comments.Add(new Comment
                {
                    FeedbackId = feedback.Id,
                    AuthorName = faker.Name.FullName(),
                    Content = faker.Lorem.Sentences(2),
                    IsOfficial = c == 0,
                    CreatedAt = faker.Date.Recent(15).ToUniversalTime(),
                });
            }
        }

        db.Comments.AddRange(comments);
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
