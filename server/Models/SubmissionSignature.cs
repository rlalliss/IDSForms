using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public sealed class SubmissionSignature
{
    public Guid Id { get; set; }

    [Required]
    public Guid SubmissionId { get; set; }

    [ForeignKey(nameof(SubmissionId))]
    public Submission Submission { get; set; } = default!;

    [Required]
    public Guid SignatureRequirementId { get; set; }

    [ForeignKey(nameof(SignatureRequirementId))]
    public SignatureRequirement SignatureRequirement { get; set; } = default!;

    public Guid? SignerUserId { get; set; }

    [ForeignKey(nameof(SignerUserId))]
    public User? SignerUser { get; set; }

    public string? SignerName { get; set; }
    public string? SignerEmail { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? SignatureImagePath { get; set; }
    public string? SourceIp { get; set; }
}

