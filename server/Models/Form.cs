public sealed class Form
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? Keywords { get; set; }
    public int Version { get; set; } = 1;
    public string? Category { get; set; }
    public string PdfBlobPath { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<FormField> Fields { get; set; } = new List<FormField>();
    public EmailTemplate? EmailTemplate { get; set; }
}






