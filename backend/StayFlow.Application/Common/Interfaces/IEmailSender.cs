namespace StayFlow.Application.Common.Interfaces;

public record EmailMessage(string To, string Subject, string HtmlBody);

/// <summary>E-posta gonderimi. Infrastructure'da SMTP (MailKit) ile; SMTP ayarlanmamissa sadece loglanir.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
