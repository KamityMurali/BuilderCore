using Dapper;
using Microsoft.Data.Sqlite;

namespace BuilderCore.Web.Data;

public sealed class DemoDataSeeder(
    ISqliteConnectionFactory connectionFactory,
    ILogger<DemoDataSeeder> logger)
{
    public const string SeedUser = "demo-seed-user";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var alreadySeeded = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM Estimate WHERE EstimateNumber = 'EST-1001';",
                cancellationToken: cancellationToken));

        if (alreadySeeded > 0)
        {
            logger.LogInformation("Demo data already seeded; skipping.");
            return;
        }

        logger.LogInformation("Seeding demo data...");
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await SeedAdditionalVendorsAsync(connection, transaction, cancellationToken);
            await SeedDemo1Async(connection, transaction, cancellationToken);
            await SeedDemo2Async(connection, transaction, cancellationToken);
            await SeedDemo3Async(connection, transaction, cancellationToken);
            await SeedDemo4Async(connection, transaction, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Demo data seeded successfully.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task SeedAdditionalVendorsAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var vendors = new[]
        {
            ("BrightLine Electric", "billing@brightline-electric.example", "555-0105"),
            ("Golden State HVAC", "service@goldenstate-hvac.example", "555-0106"),
            ("Heritage Flooring", "orders@heritage-flooring.example", "555-0107"),
            ("WestBay Painting", "estimates@westbay-painting.example", "555-0108")
        };

        foreach (var (name, email, phone) in vendors)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO Vendor (VendorName, Email, Phone, IsActive, CreatedBy, CreatedDateUtc)
                    SELECT @VendorName, @Email, @Phone, 1, @CreatedBy, @CreatedDateUtc
                    WHERE NOT EXISTS (SELECT 1 FROM Vendor WHERE VendorName = @VendorName);
                    """,
                    new
                    {
                        VendorName = name,
                        Email = email,
                        Phone = phone,
                        CreatedBy = SeedUser,
                        CreatedDateUtc = DateTime.UtcNow.ToString("O")
                    },
                    transaction,
                    cancellationToken: cancellationToken));
        }
    }

    private static async Task<int> GetCostCodeIdAsync(SqliteConnection connection, SqliteTransaction transaction, string code, CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT CostCodeId FROM CostCode WHERE Code = @Code;",
                new { Code = code },
                transaction,
                cancellationToken: cancellationToken));
    }

    private static async Task<int> GetVendorIdAsync(SqliteConnection connection, SqliteTransaction transaction, string vendorName, CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT VendorId FROM Vendor WHERE VendorName = @VendorName;",
                new { VendorName = vendorName },
                transaction,
                cancellationToken: cancellationToken));
    }

    private static async Task<int> CreateEstimateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string estimateNumber,
        string projectName,
        string status,
        DateTime createdDate,
        (string code, string description, decimal amount)[] lines,
        CancellationToken cancellationToken)
    {
        var estimateId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO Estimate (EstimateNumber, ProjectName, Description, Status, Version, CreatedBy, CreatedDateUtc)
                VALUES (@EstimateNumber, @ProjectName, @Description, @Status, 1, @CreatedBy, @CreatedDateUtc);
                SELECT last_insert_rowid();
                """,
                new
                {
                    EstimateNumber = estimateNumber,
                    ProjectName = projectName,
                    Description = $"Demo estimate for {projectName}",
                    Status = status,
                    CreatedBy = SeedUser,
                    CreatedDateUtc = createdDate.ToString("O")
                },
                transaction,
                cancellationToken: cancellationToken));

        foreach (var (code, description, amount) in lines)
        {
            var costCodeId = await GetCostCodeIdAsync(connection, transaction, code, cancellationToken);
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO EstimateLine (EstimateId, CostCodeId, Description, Quantity, UnitCost)
                    VALUES (@EstimateId, @CostCodeId, @Description, 1, @UnitCost);
                    """,
                    new
                    {
                        EstimateId = estimateId,
                        CostCodeId = costCodeId,
                        Description = description,
                        UnitCost = amount
                    },
                    transaction,
                    cancellationToken: cancellationToken));
        }

        return estimateId;
    }

    private static async Task<int> CreateJobAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string jobNumber,
        string jobName,
        int estimateId,
        string status,
        DateTime createdDate,
        CancellationToken cancellationToken)
    {
        var jobId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO Job (JobNumber, EstimateId, JobName, Status, Version, CreatedBy, CreatedDateUtc)
                VALUES (@JobNumber, @EstimateId, @JobName, @Status, 1, @CreatedBy, @CreatedDateUtc);
                SELECT last_insert_rowid();
                """,
                new
                {
                    JobNumber = jobNumber,
                    EstimateId = estimateId,
                    JobName = jobName,
                    Status = status,
                    CreatedBy = SeedUser,
                    CreatedDateUtc = createdDate.ToString("O")
                },
                transaction,
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO JobBudgetLine (JobId, CostCodeId, Description, BudgetAmount)
                SELECT @JobId, el.CostCodeId, el.Description, el.Quantity * el.UnitCost
                FROM EstimateLine el
                WHERE el.EstimateId = @EstimateId;
                """,
                new { JobId = jobId, EstimateId = estimateId },
                transaction,
                cancellationToken: cancellationToken));

        return jobId;
    }

    private static async Task<int> CreatePoAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string poNumber,
        int jobId,
        string vendorName,
        string status,
        DateTime createdDate,
        (string code, string description, decimal amount)[] lines,
        CancellationToken cancellationToken)
    {
        var vendorId = await GetVendorIdAsync(connection, transaction, vendorName, cancellationToken);
        var poId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO PurchaseOrder (PONumber, JobId, VendorId, Status, Version, CreatedBy, CreatedDateUtc)
                VALUES (@PONumber, @JobId, @VendorId, @Status, 1, @CreatedBy, @CreatedDateUtc);
                SELECT last_insert_rowid();
                """,
                new
                {
                    PONumber = poNumber,
                    JobId = jobId,
                    VendorId = vendorId,
                    Status = status,
                    CreatedBy = SeedUser,
                    CreatedDateUtc = createdDate.ToString("O")
                },
                transaction,
                cancellationToken: cancellationToken));

        foreach (var (code, description, amount) in lines)
        {
            var costCodeId = await GetCostCodeIdAsync(connection, transaction, code, cancellationToken);
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO PurchaseOrderLine (PurchaseOrderId, CostCodeId, Description, Amount)
                    VALUES (@PurchaseOrderId, @CostCodeId, @Description, @Amount);
                    """,
                    new
                    {
                        PurchaseOrderId = poId,
                        CostCodeId = costCodeId,
                        Description = description,
                        Amount = amount
                    },
                    transaction,
                    cancellationToken: cancellationToken));
        }

        return poId;
    }

    private static async Task CreateActualCostAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int jobId,
        string code,
        string? vendorName,
        int? purchaseOrderId,
        string reference,
        string description,
        decimal amount,
        DateOnly transactionDate,
        CancellationToken cancellationToken)
    {
        var costCodeId = await GetCostCodeIdAsync(connection, transaction, code, cancellationToken);
        int? vendorId = null;
        if (vendorName is not null)
        {
            vendorId = await GetVendorIdAsync(connection, transaction, vendorName, cancellationToken);
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO JobCostTransaction
                    (JobId, CostCodeId, VendorId, PurchaseOrderId, ReferenceNumber, Description,
                     TransactionDate, Amount, CreatedBy, CreatedDateUtc)
                VALUES
                    (@JobId, @CostCodeId, @VendorId, @PurchaseOrderId, @ReferenceNumber, @Description,
                     @TransactionDate, @Amount, @CreatedBy, @CreatedDateUtc);
                """,
                new
                {
                    JobId = jobId,
                    CostCodeId = costCodeId,
                    VendorId = vendorId,
                    PurchaseOrderId = purchaseOrderId,
                    ReferenceNumber = reference,
                    Description = description,
                    TransactionDate = transactionDate.ToString("O"),
                    Amount = amount,
                    CreatedBy = SeedUser,
                    CreatedDateUtc = DateTime.UtcNow.ToString("O")
                },
                transaction,
                cancellationToken: cancellationToken));
    }

    private static async Task SeedDemo1Async(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var estimateDate = DateTime.UtcNow.AddDays(-45);
        var estimateId = await CreateEstimateAsync(
            connection, transaction, "EST-1001", "1847 Willow Creek Drive", "Approved", estimateDate,
            [
                ("1000", "Site Work", 18500m),
                ("2000", "Foundation", 42000m),
                ("3000", "Framing", 78500m),
                ("4000", "Roofing", 24000m),
                ("5000", "Plumbing", 31500m),
                ("6000", "Electrical", 29000m),
                ("7000", "HVAC", 26500m),
                ("8000", "Flooring", 22000m),
                ("9000", "Painting", 18000m)
            ],
            cancellationToken);

        var jobId = await CreateJobAsync(connection, transaction, "JOB-1001", "1847 Willow Creek Drive", estimateId, "Active", estimateDate.AddDays(2), cancellationToken);

        var po1001 = await CreatePoAsync(connection, transaction, "PO-1001", jobId, "Summit Concrete Works", "Issued", DateTime.UtcNow.AddDays(-20),
            [("2000", "Foundation", 40500m)], cancellationToken);
        await CreatePoAsync(connection, transaction, "PO-1002", jobId, "Redwood Framing Co.", "Issued", DateTime.UtcNow.AddDays(-18),
            [("3000", "Framing", 75000m)], cancellationToken);
        await CreatePoAsync(connection, transaction, "PO-1003", jobId, "Pacific Crest Roofing", "Issued", DateTime.UtcNow.AddDays(-15),
            [("4000", "Roofing", 23500m)], cancellationToken);
        await CreatePoAsync(connection, transaction, "PO-1004", jobId, "ClearFlow Plumbing", "Issued", DateTime.UtcNow.AddDays(-12),
            [("5000", "Plumbing", 30800m)], cancellationToken);
        await CreatePoAsync(connection, transaction, "PO-1005", jobId, "BrightLine Electric", "Draft", DateTime.UtcNow.AddDays(-5),
            [("6000", "Electrical", 27900m)], cancellationToken);

        await CreateActualCostAsync(connection, transaction, jobId, "2000", "Summit Concrete Works", po1001, "INV-24081", "Foundation pour", 40500m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-8)), cancellationToken);
        await CreateActualCostAsync(connection, transaction, jobId, "3000", "Redwood Framing Co.", null, "INV-11842", "Framing progress", 52000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-6)), cancellationToken);
        await CreateActualCostAsync(connection, transaction, jobId, "4000", "Pacific Crest Roofing", null, "INV-77301", "Roofing deposit", 11750m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)), cancellationToken);
    }

    private static async Task SeedDemo2Async(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var estimateDate = DateTime.UtcNow.AddDays(-50);
        var estimateId = await CreateEstimateAsync(
            connection, transaction, "EST-1002", "932 Harbor View Lane", "Approved", estimateDate,
            [
                ("1000", "Site Work", 21000m),
                ("2000", "Foundation", 45000m),
                ("3000", "Framing", 82000m),
                ("4000", "Roofing", 25500m),
                ("5000", "Plumbing", 34000m),
                ("6000", "Electrical", 30000m),
                ("7000", "HVAC", 28500m),
                ("8000", "Flooring", 26000m),
                ("9000", "Painting", 19500m)
            ],
            cancellationToken);

        var jobId = await CreateJobAsync(connection, transaction, "JOB-1002", "932 Harbor View Lane", estimateId, "Active", estimateDate.AddDays(3), cancellationToken);

        await CreatePoAsync(connection, transaction, "PO-2001", jobId, "Redwood Framing Co.", "Issued", DateTime.UtcNow.AddDays(-25),
            [("3000", "Framing", 86500m)], cancellationToken);
        await CreatePoAsync(connection, transaction, "PO-2002", jobId, "BrightLine Electric", "Issued", DateTime.UtcNow.AddDays(-22),
            [("6000", "Electrical", 29200m)], cancellationToken);
        await CreatePoAsync(connection, transaction, "PO-2003", jobId, "Golden State HVAC", "Issued", DateTime.UtcNow.AddDays(-20),
            [("7000", "HVAC", 27900m)], cancellationToken);

        await CreateActualCostAsync(connection, transaction, jobId, "3000", "Redwood Framing Co.", null, "INV-55201", "Framing materials", 42000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), cancellationToken);
        await CreateActualCostAsync(connection, transaction, jobId, "6000", "BrightLine Electric", null, "INV-66102", "Rough electrical", 14500m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)), cancellationToken);
        await CreateActualCostAsync(connection, transaction, jobId, "7000", "Golden State HVAC", null, "INV-77003", "HVAC rough-in", 12000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-4)), cancellationToken);
    }

    private static async Task SeedDemo3Async(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await CreateEstimateAsync(
            connection, transaction, "EST-1003", "4172 Stonebridge Court", "Draft", DateTime.UtcNow.AddDays(-30),
            [
                ("1000", "Site Work", 19500m),
                ("2000", "Foundation", 38000m),
                ("3000", "Framing", 68000m),
                ("4000", "Roofing", 22000m),
                ("5000", "Plumbing", 28000m),
                ("6000", "Electrical", 26000m)
            ],
            cancellationToken);
    }

    private static async Task SeedDemo4Async(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var estimateDate = DateTime.UtcNow.AddDays(-90);
        var estimateId = await CreateEstimateAsync(
            connection, transaction, "EST-1004", "675 Meadow Ridge Avenue", "Approved", estimateDate,
            [
                ("1000", "Site Work", 16000m),
                ("2000", "Foundation", 36000m),
                ("3000", "Framing", 65000m),
                ("4000", "Roofing", 21000m),
                ("5000", "Plumbing", 27000m)
            ],
            cancellationToken);

        var jobId = await CreateJobAsync(connection, transaction, "JOB-1004", "675 Meadow Ridge Avenue", estimateId, "Closed", estimateDate.AddDays(5), cancellationToken);

        var po4001 = await CreatePoAsync(connection, transaction, "PO-4001", jobId, "Summit Concrete Works", "Closed", DateTime.UtcNow.AddDays(-60),
            [("2000", "Foundation", 35000m)], cancellationToken);
        await CreatePoAsync(connection, transaction, "PO-4002", jobId, "Redwood Framing Co.", "Closed", DateTime.UtcNow.AddDays(-55),
            [("3000", "Framing", 62000m)], cancellationToken);

        await CreateActualCostAsync(connection, transaction, jobId, "2000", "Summit Concrete Works", po4001, "INV-99001", "Foundation complete", 35000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-45)), cancellationToken);
        await CreateActualCostAsync(connection, transaction, jobId, "3000", "Redwood Framing Co.", null, "INV-99002", "Framing complete", 60000m, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-40)), cancellationToken);
    }
}
