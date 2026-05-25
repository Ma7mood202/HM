using HM.Domain.Enums;

namespace HM.AdminPanel.ViewModels.ShipmentRequests;

public class ShipmentRequestListItemVm
{
    public Guid                  Id                { get; set; }
    public string                RequestNumber     { get; set; } = "";
    public ShipmentRequestStatus Status            { get; set; }
    public Guid                  MerchantProfileId { get; set; }
    public DateTime              CreatedAt         { get; set; }
    public int                   OffersCount       { get; set; }
}
