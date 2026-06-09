namespace RecruitPro.Application.Common;

public static class DbDateTime
{
    public static DateTime Now => DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

    public static DateTime Today => Now.Date;

    public static int CurrentYear => Now.Year;
}
