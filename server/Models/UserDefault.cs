using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Stores default values for form fields per user.
/// Used to pre-populate PDF fields when a user fills a form.
/// </summary>
public sealed class UserDefault
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
    /// The form this default applies to.
    /// </summary>
    [Required]
    public Guid FormId { get; set; }

    [ForeignKey(nameof(FormId))]
    public Form Form { get; set; } = default!;

    /// <summary>
    /// The specific PDF field name this default applies to.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string FieldName { get; set; } = default!;

    /// <summary>
    /// The default value to prefill when the form loads.
    /// </summary>
    public string? FieldValue { get; set; }

    /// <summary>
    /// Date/time when this default was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? FormSlug { get; set; }
}
