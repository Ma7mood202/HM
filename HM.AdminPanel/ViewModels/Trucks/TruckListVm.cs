using HM.AdminPanel.ViewModels.Common;

namespace HM.AdminPanel.ViewModels.Trucks;

public class TruckListVm
{
    public TruckFilterVm Filter { get; set; } = new();
    public PagedResult<TruckListItemVm> Result { get; set; } = new();
}
