namespace SmartElderlyCare.Models;

public class DashboardStatsViewModel
{
    public int ActivePatients { get; init; }

    public int ActiveAlertsToday { get; init; }

    public int CompletedVisitsThisMonth { get; init; }

    public int VhwChecksThisMonth { get; init; }
}