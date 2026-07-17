using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.Interfaces;

namespace RecruitPro.Infrastructure.Service;

public sealed class SmtpEmailService : IEmailService
{
    private const string BrandPrimary = "#b90014";
    private const string BrandPrimaryDark = "#930614";
    private const string BrandTint = "#fff1f0";
    private const string InkStrong = "#1a1c1c";
    private const string InkMuted = "#5f5e5e";
    private const string LineSoft = "#e8e3e1";
    private const string Canvas = "#f7f6f5";
    private const string Surface = "#ffffff";
    private readonly SmtpSettings _settings;

    public SmtpEmailService(IOptions<SmtpSettings> options)
    {
        _settings = options.Value;
    }

    public Task SendCandidateInvitationAsync(string email, string fullName, string temporaryPassword, string loginUrl) =>
        SendAsync(
            email,
            "Thông tin đăng nhập RecruitPro",
            BuildCredentialEmail(
                "Thong tin dang nhap",
                fullName,
                "Tai khoan cua ban da duoc tao tren RecruitPro. Vui long dung mat khau tam thoi ben duoi de dang nhap va doi lai mat khau sau khi vao he thong.",
                temporaryPassword,
                "Dang nhap vao he thong",
                loginUrl),
            null);

    public Task SendPasswordResetAsync(string email, string fullName, string temporaryPassword, string loginUrl) =>
        SendAsync(
            email,
            "Đặt lại mật khẩu RecruitPro",
            BuildCredentialEmail(
                "Dat lai mat khau",
                fullName,
                "Ban vua yeu cau dat lai mat khau cho tai khoan RecruitPro. Vui long dang nhap bang mat khau tam thoi ben duoi va doi mat khau ngay sau khi truy cap.",
                temporaryPassword,
                "Dang nhap ngay",
                loginUrl),
            null);

    public Task SendApplicationEmailAsync(string email, string fullName, string jobTitle, string subject, string body) =>
        SendAsync(
            email,
            subject,
            BuildWorkflowEmail("Cap nhat ung tuyen", fullName, jobTitle, body),
            null);

    public Task SendOfferEmailAsync(string email, string fullName, string jobTitle, string subject, string body) =>
        SendAsync(email, subject, BuildWorkflowEmail("Thu moi nhan viec", fullName, jobTitle, body), null);

    public Task SendRejectionEmailAsync(
        string email,
        string fullName,
        string jobTitle,
        string subject,
        string body,
        string? replyToEmail) => SendAsync(email, subject, BuildWorkflowEmail("Cap nhat ket qua ung tuyen", fullName, jobTitle, body), replyToEmail);

    private async Task SendAsync(string recipient, string subject, string htmlBody, string? replyToEmail)
    {
        ValidateConfiguration();

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName, Encoding.UTF8),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = htmlBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(recipient));

        if (TryCreateMailAddress(replyToEmail, out MailAddress? replyTo))
        {
            message.ReplyToList.Add(replyTo!);
        }

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_settings.Username, _settings.Password)
        };
        await client.SendMailAsync(message);
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_settings.Host)
            || string.IsNullOrWhiteSpace(_settings.Username)
            || string.IsNullOrWhiteSpace(_settings.Password)
            || !TryCreateMailAddress(_settings.FromEmail, out _))
        {
            throw new InvalidOperationException(
                "SMTP is enabled but its configuration is incomplete. Configure Smtp__Host, Smtp__Username, Smtp__Password and Smtp__FromEmail.");
        }
    }

    private static bool TryCreateMailAddress(string? value, out MailAddress? address)
    {
        address = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            address = new MailAddress(value.Trim());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string BuildCredentialEmail(
        string eyebrow,
        string fullName,
        string intro,
        string temporaryPassword,
        string ctaLabel,
        string loginUrl)
    {
        string content = $"""
            <p style="margin:0 0 14px;">Xin chao {Encode(fullName)},</p>
            <p style="margin:0 0 16px;">{Encode(intro)}</p>
            <div style="margin:0 0 18px; padding:16px 18px; border:1px solid {LineSoft}; border-radius:14px; background:{BrandTint};">
              <div style="font-size:12px; line-height:18px; letter-spacing:0.08em; text-transform:uppercase; color:{BrandPrimaryDark}; font-weight:700; margin-bottom:8px;">
                Mat khau tam thoi
              </div>
              <div style="font-size:24px; line-height:32px; font-weight:800; color:{InkStrong};">
                {Encode(temporaryPassword)}
              </div>
            </div>
            <p style="margin:0 0 22px; color:{InkMuted};">
              De bao mat, vui long doi mat khau ngay sau khi dang nhap thanh cong.
            </p>
            <a href="{EncodeAttribute(loginUrl)}" style="{BuildButtonStyle()}">
              {Encode(ctaLabel)}
            </a>
            <p style="margin:18px 0 0; color:{InkMuted}; font-size:13px; line-height:20px;">
              Neu nut khong hoat dong, ban co the dang nhap truc tiep tai:<br />
              <a href="{EncodeAttribute(loginUrl)}" style="color:{BrandPrimary}; text-decoration:none;">{Encode(loginUrl)}</a>
            </p>
            """;

        return WrapEmailShell(eyebrow, "RecruitPro", content);
    }

    private static string BuildWorkflowEmail(string eyebrow, string fullName, string jobTitle, string body)
    {
        string content = $"""
            <p style="margin:0 0 14px;">Xin chao {Encode(fullName)},</p>
            <p style="margin:0 0 18px; color:{InkMuted};">
              RecruitPro gui den ban cap nhat lien quan den vi tri <strong style="color:{InkStrong};">{Encode(jobTitle)}</strong>.
            </p>
            <div style="margin:0 0 18px; padding:18px; border:1px solid {LineSoft}; border-radius:14px; background:{Surface};">
              {FormatMultiline(body)}
            </div>
            <p style="margin:0; color:{InkMuted}; font-size:13px; line-height:20px;">
              Ban co the tra loi truc tiep email nay neu can trao doi them.
            </p>
            """;

        return WrapEmailShell(eyebrow, jobTitle, content);
    }

    private static string WrapEmailShell(string eyebrow, string title, string content) =>
        $"""
        <!doctype html>
        <html lang="en">
          <body style="margin:0; padding:0; background:{Canvas}; font-family:Inter,Segoe UI,Roboto,Arial,sans-serif; color:{InkStrong};">
            <div style="padding:32px 16px;">
              <div style="max-width:640px; margin:0 auto;">
                <div style="margin-bottom:16px; padding:20px 24px; border-radius:20px; background:linear-gradient(135deg, {BrandPrimaryDark} 0%, {BrandPrimary} 58%, #e31b23 100%); color:#ffffff;">
                  <div style="font-size:12px; line-height:18px; letter-spacing:0.12em; text-transform:uppercase; opacity:0.82; font-weight:700;">
                    {Encode(eyebrow)}
                  </div>
                  <div style="margin-top:10px; font-size:28px; line-height:34px; font-weight:800;">
                    {Encode(title)}
                  </div>
                  <div style="margin-top:8px; font-size:14px; line-height:22px; opacity:0.9;">
                    RecruitPro Recruitment Platform
                  </div>
                </div>

                <div style="background:{Surface}; border:1px solid {LineSoft}; border-radius:20px; padding:28px 24px; box-shadow:0 20px 50px -28px rgba(26,28,28,0.22);">
                  {content}
                </div>

                <div style="padding:16px 6px 0; color:{InkMuted}; font-size:12px; line-height:18px;">
                  Email nay duoc gui tu he thong RecruitPro. Vui long khong chia se thong tin truy cap cho nguoi khac.
                </div>
              </div>
            </div>
          </body>
        </html>
        """;

    private static string FormatMultiline(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<p style=\"margin:0; color:#5f5e5e;\">No content provided.</p>";
        }

        string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] paragraphs = normalized
            .Split("\n\n", StringSplitOptions.None)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToArray();

        if (paragraphs.Length == 0)
        {
            paragraphs = [normalized.Trim()];
        }

        return string.Join(
            string.Empty,
            paragraphs.Select(paragraph =>
                $"<p style=\"margin:0 0 14px; line-height:24px; color:{InkStrong};\">{Encode(paragraph).Replace("\n", "<br />")}</p>"));
    }

    private static string BuildButtonStyle() =>
        $"display:inline-block; padding:12px 18px; border-radius:12px; background:{BrandPrimary}; color:#ffffff; text-decoration:none; font-weight:700; box-shadow:0 12px 28px -8px rgba(227,27,35,0.35);";

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string EncodeAttribute(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
