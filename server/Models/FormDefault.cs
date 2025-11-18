using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Stores default field values for a given user + form combination.
/// These defaults are applied first, and can be overridden by more specific defaults if needed.
/// </summary>
public sealed class FormDefault
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The user this default value belongs to.
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = default!;

    /// <summary>
    /// The form this default belongs to.
    /// </summary>
    [Required]
    public Guid FormId { get; set; }

    [ForeignKey(nameof(FormId))]
    public Form Form { get; set; } = default!;

    /// <summary>
    /// The name of the PDF field (matches FormField.PdfFieldName).
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string FieldName { get; set; } = default!;

    /// <summary>
    /// The default value applied before user-level overrides.
    /// </summary>
    public string? FieldValue { get; set; }

    /// <summary>
    /// When this default value was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
