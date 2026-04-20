using Bogus;
using Feedback.Domain;
using Feedback.Infrastructure.Persistence;
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

        const int totalFeedbacks = 10_000;
        const int batchSize = 500;

        var allFeedbacks = new List<FeedbackItem>(totalFeedbacks);

        for (var i = 0; i < totalFeedbacks; i++)
        {
            allFeedbacks.Add(new FeedbackItem
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

        // Insert feedbacks in batches to avoid memory pressure
        for (var batch = 0; batch < totalFeedbacks; batch += batchSize)
        {
            var chunk = allFeedbacks.Skip(batch).Take(batchSize).ToList();
            db.Feedbacks.AddRange(chunk);
            await db.SaveChangesAsync();
        }

        // Add votes for 5,000 feedbacks (one vote per voter per feedback, unique pairs)
        var votes = new List<Vote>();
        var voteSet = new HashSet<(int, string)>();

        foreach (var feedback in allFeedbacks.Take(5_000))
        {
            var voterCount = random.Next(1, 8);
            for (var v = 0; v < voterCount; v++)
            {
                var email = $"voter-{Guid.NewGuid():N}@example.com";
                if (voteSet.Add((feedback.Id, email.ToLower())))
                {
                    votes.Add(new Vote
                    {
                        FeedbackId = feedback.Id,
                        VoterEmail = email,
                        CreatedAt = faker.Date.Recent(90).ToUniversalTime(),
                    });
                    feedback.VoteCount++;
                }

                if (votes.Count >= batchSize)
                {
                    db.Votes.AddRange(votes);
                    await db.SaveChangesAsync();
                    votes.Clear();
                }
            }
        }

        if (votes.Count > 0)
        {
            db.Votes.AddRange(votes);
            await db.SaveChangesAsync();
        }

        // Sync VoteCount for feedbacks that got votes
        await db.SaveChangesAsync();

        // Add comments for 2,000 feedbacks
        var comments = new List<Comment>();
        foreach (var feedback in allFeedbacks.Take(2_000))
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
                    CreatedAt = faker.Date.Recent(60).ToUniversalTime(),
                });

                if (comments.Count >= batchSize)
                {
                    db.Comments.AddRange(comments);
                    await db.SaveChangesAsync();
                    comments.Clear();
                }
            }
        }

        if (comments.Count > 0)
        {
            db.Comments.AddRange(comments);
            await db.SaveChangesAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
