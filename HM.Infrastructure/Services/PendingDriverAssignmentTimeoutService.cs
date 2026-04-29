using System.Text.Json;
using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using HM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HM.Infrastructure.Services;

/// <summary>
/// Polls every 60s for shipments stuck in PendingDriverAcceptance past the timeout window
/// (default 15 min). Reverts each to AwaitingDriver, clears DriverProfileId, and notifies
/// the truck account so they can pick a different driver.
/// </summary>
public sealed class PendingDriverAssignmentTimeoutService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AcceptanceTimeout = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _services;
    private readonly ILogger<PendingDriverAssignmentTimeoutService> _logger;

    public PendingDriverAssignmentTimeoutService(IServiceProvider services, ILogger<PendingDriverAssignmentTimeoutService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredAssignmentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PendingDriverAssignmentTimeoutService iteration failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Shutdown
            }
        }
    }

    private async Task ProcessExpiredAssignmentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var cutoff = DateTime.UtcNow - AcceptanceTimeout;
        var expired = await db.Shipments
            .Where(s => s.Status == ShipmentStatus.PendingDriverAcceptance
                        && s.AssignedAt != null
                        && s.AssignedAt < cutoff)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0) return;

        foreach (var shipment in expired)
        {
            shipment.Status = ShipmentStatus.AwaitingDriver;
            shipment.DriverProfileId = null;
            shipment.AssignedAt = null;
        }
        // Audit trail: if the host crashes between commit and notification dispatch, the
        // shipments are reverted but the truck account never hears about it. Log the IDs
        // first so we can reconstruct what happened from logs.
        _logger.LogInformation("Reverting {Count} timed-out shipments: {ShipmentIds}", expired.Count, expired.Select(s => s.Id));
        await db.SaveChangesAsync(cancellationToken);

        foreach (var shipment in expired)
        {
            try
            {
                var offer = await db.ShipmentOffers.FindAsync([shipment.AcceptedOfferId], cancellationToken);
                if (offer == null) continue;
                var truckAccount = await db.TruckAccounts.FindAsync([offer.TruckAccountId], cancellationToken);
                if (truckAccount == null) continue;
                var data = JsonSerializer.Serialize(new { shipmentId = shipment.Id });
                await notifications.SendNotificationAsync(
                    truckAccount.UserId,
                    "انتهت مهلة قبول السائق",
                    "لم يقم السائق بالرد خلال 15 دقيقة. يرجى تعيين سائق آخر.",
                    data,
                    true,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify truck account for timed-out shipment {ShipmentId}", shipment.Id);
            }
        }

        _logger.LogInformation("Timed out {Count} pending driver assignments", expired.Count);
    }
}
