using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using StayFlow.Application.Common.Interfaces;

namespace StayFlow.Infrastructure.Email;

public class EmailSettings
{
    /// <summary>Bos birakilirsa e-posta gonderilmez, sadece loglanir (gelistirme/test).</summary>
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "noreply@stayflow.app";
    public string FromName { get; set; } = "StayFlow";
    /// <summary>Mailpit gibi yerel test sunuculari TLS desteklemez.</summary>
    public bool UseTls { get; set; } = true;
}

/// <summary>MailKit ile SMTP gonderimi (System.Net.Mail.SmtpClient Microsoft tarafindan artik onerilmiyor).</summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;

    public SmtpEmailSender(EmailSettings settings)
    {
        _settings = settings;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = WrapHtml(message.HtmlBody) }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port,
            _settings.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
        if (!string.IsNullOrEmpty(_settings.UserName))
        {
            await client.AuthenticateAsync(_settings.UserName, _settings.Password, ct);
        }
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }

    private static string WrapHtml(string body) => $"""
        <!doctype html><html lang="tr"><body style="font-family:Segoe UI,Arial,sans-serif;color:#1e293b;line-height:1.5">
        {body}
        <p style="color:#94a3b8;font-size:12px;margin-top:24px">Bu e-posta StayFlow tarafından otomatik gönderilmiştir.</p>
        </body></html>
        """;
}

/// <summary>SMTP ayarlanmamissa: e-postayi gondermek yerine loglar. Arka plan isleri yine de calisir.</summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation("E-posta (SMTP ayarli degil, gonderilmedi) -> {To}: {Subject}", message.To, message.Subject);
        return Task.CompletedTask;
    }
}
