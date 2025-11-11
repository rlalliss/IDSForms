using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public sealed class UserProfile
{
    [Key]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = default!;

    public string FullName { get; set; } = default!;
    public string? Company { get; set; }
    public string Email { get; set; } = default!;
    public string? SecondaryEmails { get; set; }
}
