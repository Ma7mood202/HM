namespace HM.AdminPanel.ViewModels.TruckAccounts;

public class TruckAccountListItemVm
{
    public Guid     Id          { get; set; }
    public string   FullName    { get; set; } = "";
    public string   PhoneNumber { get; set; } = "";
    public string   Email       { get; set; } = "";
    public bool     IsActive    { get; set; }
    public bool     IsBlocked   { get; set; }
    public DateTime CreatedAt   { get; set; }
}
