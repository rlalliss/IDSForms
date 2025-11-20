using Microsoft.EntityFrameworkCore;

public sealed class AppDb : DbContext {
  public AppDb(DbContextOptions<AppDb> o): base(o) {}
  public DbSet<User> Users => Set<User>();
  public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
  public DbSet<UserDefault> UserDefaults => Set<UserDefault>();
  public DbSet<Form> Forms => Set<Form>();
  public DbSet<FormField> FormFields => Set<FormField>();
  public DbSet<FormDefault> FormDefaults => Set<FormDefault>();
  public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
  public DbSet<Submission> Submissions => Set<Submission>();
}
