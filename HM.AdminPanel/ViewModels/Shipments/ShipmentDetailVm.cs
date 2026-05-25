using HM.Domain.Enums;

namespace HM.AdminPanel.ViewModels.Shipments;

public class ShipmentDetailVm
{
    public Guid           Id                 { get; set; }
    public ShipmentStatus Status             { get; set; }
    public DateTime?      StartedAt          { get; set; }
    public DateTime?      CompletedAt        { get; set; }
    public DateTime?      AssignedAt         { get; set; }
    public double?        CurrentLat         { get; set; }
    public double?        CurrentLng         { get; set; }
    public DateTime?      LocationUpdatedAt  { get; set; }
    public Guid           ShipmentRequestId  { get; set; }
    public Guid           AcceptedOfferId    { get; set; }
    public Guid           TruckId            { get; set; }
    public Guid?          DriverProfileId    { get; set; }
}
