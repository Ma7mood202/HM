using HM.Domain.Enums;

namespace HM.AdminPanel.ViewModels.Trucks;

public class TruckDetailVm
{
    public Guid                Id              { get; set; }
    public string              PlateNumber     { get; set; } = "";
    public TruckType           TruckType       { get; set; }
    public TruckBodyType?      BodyType        { get; set; }
    public decimal             MaxWeight       { get; set; }
    public bool                IsActive        { get; set; }
    public TruckApprovalStatus ApprovalStatus  { get; set; }
    public string?             RejectionReason { get; set; }
    public Guid                TruckAccountId  { get; set; }
    public string              OwnerName       { get; set; } = "";
    public int                 ShipmentsCount  { get; set; }
}
