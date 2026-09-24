using SmartElderlyCare.Models;

namespace SmartElderlyCare.Services;

public interface IDistrictReportService
{
    Task<DistrictReport> GenerateOrUpdateReportAsync(
        int year,
        int month,
        string districtCode,
        string districtName,
        string userId,
        CancellationToken cancellationToken = default);
}