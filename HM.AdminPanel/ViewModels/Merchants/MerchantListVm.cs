using HM.AdminPanel.ViewModels.Common;

namespace HM.AdminPanel.ViewModels.Merchants;

public class MerchantListVm
{
    public MerchantFilterVm Filter { get; set; } = new();
    public PagedResult<MerchantListItemVm> Result { get; set; } = new();
}
