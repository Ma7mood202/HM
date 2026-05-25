using HM.Domain.Enums;

namespace HM.AdminPanel.ViewModels.Shipments;

public class ShipmentListItemVm
{
    public Guid           Id          { get; set; }
    public ShipmentStatus Status      { get; set; }
    public DateTime?      StartedAt   { get; set; }
    public DateTime?      CompletedAt { get; set; }
    public Guid           TruckId     { get; set; }
    public Guid?          DriverId    { get; set; }
}
