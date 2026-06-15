namespace RecruitPro.Application.DTOs.Response;

public class CandidateCertificationDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public DateTime? IssuedOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public string? CredentialId { get; set; }
    public string? CredentialUrl { get; set; }
}
