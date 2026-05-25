using HM.Domain.Enums;

namespace HM.AdminPanel.ViewModels.Trucks;

public class TruckFilterVm
{
    public TruckApprovalStatus? Status   { get; set; }
    public string?              Search   { get; set; }
    public int                  Page     { get; set; } = 1;
    public int                  PageSize { get; set; } = 25;
}
