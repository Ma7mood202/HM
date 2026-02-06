using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Query for listing merchant shipment requests (tabs, search, date range).
/// </summary>
public class GetMerchantShipmentRequestsQuery
{
    public ShipmentRequestStatus? Status { get; set; }
    public string? Cursor { get; set; }
    public int PageSize { get; set; } = 10;
    public int PageNumber { get; set; } = 1;
    public string? Search { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}
