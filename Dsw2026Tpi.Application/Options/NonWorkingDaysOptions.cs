namespace Dsw2026Tpi.Application.Options
{
    public sealed class NonWorkingDaysOptions
    {
        public const string SectionName = "NonWorkingDays";
        public List<string> Dates { get; set; } = [];
    }
}
