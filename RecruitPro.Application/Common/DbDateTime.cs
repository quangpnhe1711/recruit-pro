namespace RecruitPro.Application.Common;

public static class DbDateTime
{
    /// <summary>
    /// Gets the current database-friendly date and time value.
    /// </summary>
    public static DateTime Now => DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

    /// <summary>
    /// Gets the current database-friendly date value.
    /// </summary>
    public static DateTime Today => Now.Date;

    /// <summary>
    /// Gets the current database-friendly year value.
    /// </summary>
    public static int CurrentYear => Now.Year;
}
