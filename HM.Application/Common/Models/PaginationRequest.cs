namespace HM.Application.Common.Models;

/// <summary>
/// Request model for paginated queries.
/// </summary>
public class PaginationRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
