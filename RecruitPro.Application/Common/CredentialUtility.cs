namespace RecruitPro.Application.Common;

public static class CredentialUtility
{
    public static string GenerateTemporaryPassword()
    {
        return $"Rp!{Guid.NewGuid():N}"[..12];
    }
}
