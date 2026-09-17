namespace BuilderCore.Web.Models;

public sealed class CostCode
{
    public int CostCodeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class Vendor
{
    public int VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Estimate
{
    public int EstimateId { get; set; }
    public string EstimateNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Draft";
    public int Version { get; set; } = 1;
    public int? JobId { get; set; }
}

public sealed class EstimateLine
{
    public int EstimateLineId { get; set; }
    public int EstimateId { get; set; }
    public int CostCodeId { get; set; }
    public string? CostCode { get; set; }
    public string? CostCodeDescription { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineAmount => Quantity * UnitCost;
}

public sealed class Job
{
    public int JobId { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public int EstimateId { get; set; }
    public string? EstimateNumber { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public int Version { get; set; } = 1;
}

public sealed class JobBudgetLine
{
    public int JobBudgetLineId { get; set; }
    public int JobId { get; set; }
    public int CostCodeId { get; set; }
    public string? CostCode { get; set; }
    public string? CostCodeDescription { get; set; }
    public string? Description { get; set; }
    public decimal BudgetAmount { get; set; }
}

public sealed class PurchaseOrder
{
    public int PurchaseOrderId { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public int JobId { get; set; }
    public string? JobNumber { get; set; }
    public string? JobName { get; set; }
    public int VendorId { get; set; }
    public string? VendorName { get; set; }
    public string Status { get; set; } = "Draft";
    public int Version { get; set; } = 1;
}

public sealed class PurchaseOrderLine
{
    public int PurchaseOrderLineId { get; set; }
    public int PurchaseOrderId { get; set; }
    public int CostCodeId { get; set; }
    public string? CostCode { get; set; }
    public string? CostCodeDescription { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
}

public sealed class JobCostTransaction
{
    public int JobCostTransactionId { get; set; }
    public int JobId { get; set; }
    public int CostCodeId { get; set; }
    public string? CostCode { get; set; }
    public string? CostCodeDescription { get; set; }
    public int? VendorId { get; set; }
    public string? VendorName { get; set; }
    public int? PurchaseOrderId { get; set; }
    public string? PONumber { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    public DateOnly TransactionDate { get; set; }
    public decimal Amount { get; set; }
}

public sealed record JobCostLineDto(
    int CostCodeId,
    string CostCode,
    string Description,
    decimal Budget,
    decimal Committed,
    decimal Actual,
    decimal Variance);

public sealed record JobCostSummaryDto(
    int JobId,
    string JobNumber,
    string JobName,
    IReadOnlyList<JobCostLineDto> Lines,
    decimal Budget,
    decimal Committed,
    decimal Actual,
    decimal Variance);

public sealed class DashboardStats
{
    public int ActiveJobs { get; set; }
    public int DraftEstimates { get; set; }
    public int OpenPurchaseOrders { get; set; }
    public IReadOnlyList<JobCostSummaryDto> RecentJobs { get; set; } = [];
}
