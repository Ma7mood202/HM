using HM.AdminPanel.ViewModels.Common;

namespace HM.AdminPanel.ViewModels.Shipments;

public class ShipmentListVm
{
    public ShipmentFilterVm Filter { get; set; } = new();
    public PagedResult<ShipmentListItemVm> Result { get; set; } = new();
}
