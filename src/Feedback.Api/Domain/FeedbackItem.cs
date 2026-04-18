namespace Feedback.Api.Domain;

public class FeedbackItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public FeedbackType Type { get; set; }
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Open;
    public FeedbackPriority Priority { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int VoteCount { get; set; }

    public List<Vote> Votes { get; set; } = [];
    public List<Comment> Comments { get; set; } = [];
}

public enum FeedbackType
{
    Bug,
    Feature,
    Improvement
}

public enum FeedbackStatus
{
    Open,
    UnderReview,
    Planned,
    InProgress,
    Done,
    Closed
}

public enum FeedbackPriority
{
    Low,
    Medium,
    High
}
