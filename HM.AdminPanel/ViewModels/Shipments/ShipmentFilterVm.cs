using HM.Domain.Enums;

namespace HM.AdminPanel.ViewModels.Shipments;

public class ShipmentFilterVm
{
    public ShipmentStatus? Status     { get; set; }
    public DateTime?       From       { get; set; }
    public DateTime?       To         { get; set; }
    public Guid?           MerchantId { get; set; }
    public Guid?           DriverId   { get; set; }
    public int             Page       { get; set; } = 1;
    public int             PageSize   { get; set; } = 25;
}
