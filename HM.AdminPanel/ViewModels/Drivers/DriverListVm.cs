using HM.AdminPanel.ViewModels.Common;

namespace HM.AdminPanel.ViewModels.Drivers;

public class DriverListVm
{
    public DriverFilterVm Filter { get; set; } = new();
    public PagedResult<DriverListItemVm> Result { get; set; } = new();
}
