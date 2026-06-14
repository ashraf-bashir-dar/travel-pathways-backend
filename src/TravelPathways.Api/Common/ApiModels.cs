namespace TravelPathways.Api.Common;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public string[]? Errors { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message, params string[] errors) =>
        new() { Success = false, Message = message, Errors = errors is { Length: > 0 } ? errors : null };
}

public sealed class PaginatedResponse<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public required int TotalPages { get; init; }
    /// <summary>When set (e.g. payments list), sum of a numeric field across the full filtered result, not just the current page.</summary>
    public decimal? TotalAmount { get; init; }
    /// <summary>Sales confirmed packages: sum of expected profit across the full filtered result.</summary>
    public decimal? TotalExpectedProfit { get; init; }
    /// <summary>Sales confirmed packages: sum of actual profit (non-null rows) across the full filtered result.</summary>
    public decimal? TotalActualProfit { get; init; }
}

