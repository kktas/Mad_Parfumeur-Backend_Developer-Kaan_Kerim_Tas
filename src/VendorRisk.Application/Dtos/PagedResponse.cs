namespace VendorRisk.Application.Dtos;

/// <summary>Envelope for paged list endpoints.</summary>
public sealed class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
