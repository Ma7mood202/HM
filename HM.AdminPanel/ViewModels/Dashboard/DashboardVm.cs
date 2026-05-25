namespace HM.AdminPanel.ViewModels.Dashboard;

public class DashboardVm
{
    public int TotalMerchants     { get; set; }
    public int TotalDrivers       { get; set; }
    public int TotalTruckAccounts { get; set; }
    public int TotalTrucks        { get; set; }
    public int ActiveShipments    { get; set; }
    public int CompletedToday     { get; set; }

    public List<DayCount>    Last30Days        { get; set; } = new();
    public List<StatusCount> ShipmentsByStatus { get; set; } = new();
    public List<RecentItem>  RecentActivity    { get; set; } = new();
}

public record DayCount(DateOnly Day, int Count);
public record StatusCount(string Status, int Count);
public record RecentItem(string Kind, string Title, string Subtitle, DateTime At);
