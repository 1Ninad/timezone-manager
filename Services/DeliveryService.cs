// DeliveryService.cs
// All database reads and writes for delivery records go through this class.
// Pages call methods here instead of talking to the database directly — keeps DB
// logic in one place and makes pages easier to read.
//
// Why IDbContextFactory instead of injecting AppDbContext directly:
// Blazor Server keeps a persistent connection open per browser tab. If we shared one
// DbContext across all connections it would cause concurrency errors. The factory creates
// a fresh, short-lived DbContext for each operation and disposes it immediately after —
// this is the standard safe pattern for Blazor Server apps.

using Microsoft.EntityFrameworkCore;
using timezone_manager.Models;

namespace timezone_manager.Services;

public class DeliveryService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DeliveryService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    // Saves a new delivery record to the database.
    // timestampUtc must already be in UTC — the caller (NewEntry.razor) gets this from JavaScript.
    // submitterTimezone is the IANA ID captured from the browser, e.g. "Europe/Stockholm".
    public async Task SaveRecordAsync(
        long deliveryNumber,
        string plant,
        string material,
        DateTime timestampUtc,
        string submitterTimezone)
    {
        await using var db = await _factory.CreateDbContextAsync();

        db.DeliveryRecords.Add(new DeliveryRecord
        {
            DeliveryNumber = deliveryNumber,
            Plant = plant,
            Material = material,
            // SpecifyKind ensures EF Core stores this as UTC, not as unspecified/local.
            TimestampUtc = DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc),
            SubmitterTimezone = submitterTimezone
        });

        await db.SaveChangesAsync();
    }

    // Returns all records ordered newest-first, so the most recent entries appear at the top.
    public async Task<List<DeliveryRecord>> GetAllRecordsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.DeliveryRecords
            .OrderByDescending(r => r.TimestampUtc)
            .ToListAsync();
    }
}
