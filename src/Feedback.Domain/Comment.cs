namespace Feedback.Domain;

public class Comment
{
    public int Id { get; set; }
    public int FeedbackId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsOfficial { get; set; }

    public FeedbackItem Feedback { get; set; } = null!;
}
