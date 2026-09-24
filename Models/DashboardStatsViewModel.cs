namespace SmartElderlyCare.Models;

public class DashboardStatsViewModel
{
    public int ActivePatients { get; init; }

    public int ActiveAlertsToday { get; init; }

    public int CompletedVisitsThisMonth { get; init; }

    public int VhwChecksThisMonth { get; init; }

    public IReadOnlyList<TodayActivityItem> TodayActivity { get; init; } = [];

    public IReadOnlyList<ConditionBreakdownItem> ConditionBreakdown { get; init; } = [];

    public IReadOnlyList<AttentionItem> NeedsAttention { get; init; } = [];
}

public sealed class TodayActivityItem
{
    public string Type { get; init; } = string.Empty;

    public string PatientName { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public DateTimeOffset RecordedAt { get; init; }
}

public sealed class ConditionBreakdownItem
{
    public string Condition { get; init; } = string.Empty;

    public int Count { get; init; }
}

public sealed class AttentionItem
{
    public string Type { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string PatientName { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}