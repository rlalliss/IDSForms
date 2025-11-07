public sealed class EmailTemplate
{
    public Guid Id { get; set; }
    public Guid FormId { get; set; }
    public string Subject { get; set; } = default!;
    public string BodyHtml { get; set; } = default!;
    public string To { get; set; } = default!;
    public string? Cc { get; set; }
    public string? Bcc { get; set; }
    public string? FromOverride { get; set; }
    public bool AttachFlattened { get; set; }
}