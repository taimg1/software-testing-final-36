namespace Feedback.Domain;

public class Vote
{
    public int Id { get; set; }
    public int FeedbackId { get; set; }
    public string VoterEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public FeedbackItem Feedback { get; set; } = null!;
}
