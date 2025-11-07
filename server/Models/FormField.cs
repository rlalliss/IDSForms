public sealed class FormField
{
    public Guid Id { get; set; }
    public Guid FormId { get; set; }
    public string PdfFieldName { get; set; } = default!;
    public string Label { get; set; } = default!;
    public string Type { get; set; } = "text";
    public string? Placeholder { get; set; }
    public bool Required { get; set; }
    public string? Regex { get; set; }
    public int? Min { get; set; }
    public int? Max { get; set; }
    public string? OptionsJson { get; set; }
    public string? DefaultValue { get; set; }
    public string? GroupName { get; set; }
    public int OrderIndex { get; set; }
}