namespace HM.AdminPanel.ViewModels.Drivers;

public class DriverDetailVm
{
    public Guid     Id                       { get; set; }
    public string   FullName                 { get; set; } = "";
    public string   PhoneNumber              { get; set; } = "";
    public string   Email                    { get; set; } = "";
    public bool     IsActive                 { get; set; }
    public bool     IsBlocked                { get; set; }
    public DateTime? BlockedAt               { get; set; }
    public string?  BlockedReason            { get; set; }
    public DateTime CreatedAt                { get; set; }
    public int      CompletedShipmentCount   { get; set; }
}
