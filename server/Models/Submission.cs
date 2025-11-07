public sealed class Submission
{
    public Guid Id { get; set; }
    public Guid FormId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string PayloadJson { get; set; } = default!;
    public string PdfPath { get; set; } = default!;
    public string? EmailMessageId { get; set; }
}