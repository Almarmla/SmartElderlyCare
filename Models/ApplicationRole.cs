namespace SmartElderlyCare.Models;

public enum ApplicationRole
{
    Nurse,
    FacilityNurse,
    VHW,
    Family,
    RecordsStaff,
    HiuClerk,
    DhioAdmin,
    Administrator,
    Dmo
}

public static class ApplicationRoles
{
    public const string Nurse = nameof(ApplicationRole.Nurse);
    public const string FacilityNurse = nameof(ApplicationRole.FacilityNurse);
    public const string VHW = nameof(ApplicationRole.VHW);
    public const string Family = nameof(ApplicationRole.Family);
    public const string RecordsStaff = nameof(ApplicationRole.RecordsStaff);
    public const string HiuClerk = nameof(ApplicationRole.HiuClerk);
    public const string DhioAdmin = nameof(ApplicationRole.DhioAdmin);
    public const string Administrator = nameof(ApplicationRole.Administrator);
    public const string Dmo = nameof(ApplicationRole.Dmo);
}
