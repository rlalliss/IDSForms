public interface IEmailService {
  Task<string?> SendAsync(string toCsv, string subject, string html, string? attachmentPath);
}

public sealed class EmailService : IEmailService {
  private readonly IConfiguration _cfg;
  public EmailService(IConfiguration cfg) => _cfg = cfg;
  public async Task<string?> SendAsync(string toCsv, string subject, string html, string? attachmentPath) {
    var msg = new MimeKit.MimeMessage();
    msg.From.Add(new MimeKit.MailboxAddress(_cfg["Smtp:FromName"], _cfg["Smtp:FromEmail"]));
    foreach (var a in toCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
      msg.To.Add(MimeKit.MailboxAddress.Parse(a.Trim()));
    msg.Subject = subject;
    var builder = new MimeKit.BodyBuilder { HtmlBody = html };
    if (!string.IsNullOrWhiteSpace(attachmentPath)) builder.Attachments.Add(attachmentPath);
    msg.Body = builder.ToMessageBody();

    using var client = new MailKit.Net.Smtp.SmtpClient();
    await client.ConnectAsync(_cfg["Smtp:Host"], int.Parse(_cfg["Smtp:Port"] ?? ""), _cfg.GetValue<bool>("Smtp:UseSsl"));
    if (!string.IsNullOrEmpty(_cfg["Smtp:User"]))
      await client.AuthenticateAsync(_cfg["Smtp:User"], _cfg["Smtp:Pass"]);
    await client.SendAsync(msg);
    await client.DisconnectAsync(true);
    return msg.MessageId;
  }
}
