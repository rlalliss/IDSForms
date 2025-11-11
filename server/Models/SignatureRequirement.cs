using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public sealed class SignatureRequirement
{
    public Guid Id { get; set; }

    [Required]
    public Guid FormId { get; set; }

    [ForeignKey(nameof(FormId))]
    public Form Form { get; set; } = default!;

    [Required]
    public string Name { get; set; } = default!;

    [Required]
    public string PdfFieldName { get; set; } = default!;

    [Required]
    public string SignerRole { get; set; } = default!;

    public int OrderIndex { get; set; } = 0;
    public bool Required { get; set; } = true;
}

