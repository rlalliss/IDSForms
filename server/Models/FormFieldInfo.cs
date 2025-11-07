namespace PdfApp.Api.Models;

public sealed class FormFieldInfo
{
    public string Name { get; init; } = default!;
    public string Type { get; init; } = "text";
    public bool Required { get; init; }
    public int PageNumber { get; init; }  // 1-based
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; }
    public float Height { get; init; }
}
