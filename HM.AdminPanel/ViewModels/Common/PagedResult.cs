namespace HM.AdminPanel.ViewModels.Common;

public class PagedResult<T>
{
    public List<T> Items     { get; set; } = new();
    public int     Page      { get; set; } = 1;
    public int     PageSize  { get; set; } = 25;
    public int     Total     { get; set; }
    public int     TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
}
