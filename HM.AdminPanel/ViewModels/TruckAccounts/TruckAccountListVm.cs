using HM.AdminPanel.ViewModels.Common;

namespace HM.AdminPanel.ViewModels.TruckAccounts;

public class TruckAccountListVm
{
    public TruckAccountFilterVm Filter { get; set; } = new();
    public PagedResult<TruckAccountListItemVm> Result { get; set; } = new();
}
