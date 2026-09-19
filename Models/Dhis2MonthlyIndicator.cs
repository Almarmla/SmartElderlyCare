namespace SmartElderlyCare.Models;

public class Dhis2MonthlyIndicator
{
    public string DataElement { get; set; } = string.Empty;

    public string Period { get; set; } = string.Empty;

    public string OrganisationUnit { get; set; } = string.Empty;

    public decimal Value { get; set; }
}
