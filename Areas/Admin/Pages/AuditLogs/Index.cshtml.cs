using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartElderlyCare.Data;
using SmartElderlyCare.Models;

namespace SmartElderlyCare.Areas.Admin.Pages.AuditLogs;

[Authorize(AuthenticationSchemes = "AdminCookie,Identity.Application", Roles = ApplicationRoles.DhioAdmin + "," + ApplicationRoles.Administrator)]
public class IndexModel : PageModel
{
    private const int PageSize = 25;

    private readonly ApplicationDbContext _dbContext;

    public IndexModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IReadOnlyList<AdministrationAuditLog> Logs { get; private set; } = [];

    public int PageNumber { get; private set; } = 1;

    public int PageCount { get; private set; } = 1;

    public int TotalCount { get; private set; }

    public IReadOnlyList<string> Actions { get; } = Enum.GetNames<AdministrationAction>();

    [BindProperty(SupportsGet = true)]
    public AdministrationAction? Action { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTimeOffset? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTimeOffset? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken, int pageNumber = 1)
    {
        var query = _dbContext.AdministrationAuditLogs
            .AsNoTracking()
            .Include(log => log.PerformedByUser)
            .AsQueryable();

        if (Action is { } action)
        {
            query = query.Where(log => log.Action == action);
        }

        if (From is { } from)
        {
            query = query.Where(log => log.PerformedAt >= from.ToUniversalTime());
        }

        if (To is { } to)
        {
            var toExclusive = to.Date.AddDays(1).ToUniversalTime();
            query = query.Where(log => log.PerformedAt < toExclusive);
        }

        Search = Search?.Trim();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var search = Search;
            query = query.Where(log =>
                log.PerformedByUser.DisplayName.Contains(search) ||
                (log.PerformedByUser.Email != null && log.PerformedByUser.Email.Contains(search)));
        }

        TotalCount = await query.CountAsync(cancellationToken);
        PageCount = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        PageNumber = Math.Clamp(pageNumber, 1, PageCount);

        Logs = await query
            .OrderByDescending(log => log.PerformedAt)
            .ThenByDescending(log => log.Id)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);
    }

    public Dictionary<string, string?> PageRoute(int pageNumber) => new()
    {
        ["pageNumber"] = pageNumber.ToString(),
        ["Action"] = Action?.ToString(),
        ["From"] = From?.ToString("yyyy-MM-dd"),
        ["To"] = To?.ToString("yyyy-MM-dd"),
        ["Search"] = Search
    };
}