using HM.AdminPanel.ViewModels.Dashboard;

namespace HM.AdminPanel.Services;

public interface IDashboardQueryService
{
    Task<DashboardVm> BuildAsync(CancellationToken ct = default);
}
