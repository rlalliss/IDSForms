public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Company { get; set; }
    public string Email { get; set; } = default!;
    public string? SecondaryEmails { get; set; }
}
