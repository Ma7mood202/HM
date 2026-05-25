namespace HM.AdminPanel.ViewModels.TruckAccounts;

public class TruckAccountFilterVm
{
    public string? Search    { get; set; }
    public bool?   IsBlocked { get; set; }
    public bool?   IsActive  { get; set; }
    public int     Page      { get; set; } = 1;
    public int     PageSize  { get; set; } = 25;
}
