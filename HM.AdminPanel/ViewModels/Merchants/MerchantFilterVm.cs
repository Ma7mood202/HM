namespace HM.AdminPanel.ViewModels.Merchants;

public class MerchantFilterVm
{
    public string? Search    { get; set; }
    public bool?   IsBlocked { get; set; }
    public bool?   IsActive  { get; set; }
    public int     Page      { get; set; } = 1;
    public int     PageSize  { get; set; } = 25;
}
