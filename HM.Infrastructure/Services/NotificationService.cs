using System.Collections.Generic;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using HM.Application.Common.DTOs.Notification;
using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using HM.Domain.Entities;
using HM.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HM.Infrastructure.Services;

public sealed class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _db;
    private readonly FirebaseOptions _firebaseOptions;
    private static bool _firebaseAppInitialized;

    public NotificationService(IApplicationDbContext db, IOptions<FirebaseOptions> firebaseOptions)
    {
        _db = db;
        _firebaseOptions = firebaseOptions.Value;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var list = await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Body = n.Body,
                Data = n.Data,
                SeenAt = n.SeenAt,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<int> GetUnseenCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Notifications
            .CountAsync(n => n.UserId == userId && n.SeenAt == null, cancellationToken);
    }

    public async Task<NotificationDto?> GetByIdAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var n = await _db.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, cancellationToken);
        return n == null ? null : new NotificationDto
        {
            Id = n.Id,
            Title = n.Title,
            Body = n.Body,
            Data = n.Data,
            SeenAt = n.SeenAt,
            CreatedAt = n.CreatedAt
        };
    }

    public async Task MarkAsSeenAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var n = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, cancellationToken);
        if (n == null) return;
        if (n.SeenAt == null)
            n.SeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsSeenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId && n.SeenAt == null)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var n in notifications)
            n.SeenAt = now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default)
    {
        var n = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, cancellationToken);
        if (n != null)
        {
            _db.Notifications.Remove(n);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var toRemove = await _db.Notifications
            .Where(n => n.UserId == userId)
            .ToListAsync(cancellationToken);
        _db.Notifications.RemoveRange(toRemove);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RegisterDeviceAsync(Guid userId, RegisterDeviceRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return;
        var platform = string.IsNullOrWhiteSpace(request.Platform) ? "android" : request.Platform.Trim().ToLowerInvariant();

        var existing = await _db.FcmDeviceTokens
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == request.Token, cancellationToken);
        if (existing != null)
        {
            existing.Platform = platform;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.FcmDeviceTokens.Add(new FcmDeviceToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = request.Token.Trim(),
                Platform = platform,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SendNotificationAsync(Guid userId, string title, string body, string? data = null, bool sendPush = true, CancellationToken cancellationToken = default)
    {
        var notification = new HM.Domain.Entities.Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Body = body,
            Data = data,
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);

        if (sendPush)
            await SendPushToUserAsync(userId, title, body, data, notification.Id, cancellationToken);
    }

    private void EnsureFirebaseApp()
    {
        if (_firebaseAppInitialized)
            return;
        lock (typeof(NotificationService))
        {
            if (_firebaseAppInitialized)
                return;
            if (string.IsNullOrWhiteSpace(_firebaseOptions.CredentialsPath) || !File.Exists(_firebaseOptions.CredentialsPath))
                return;
            try
            {
                if (FirebaseApp.DefaultInstance == null)
                {
                    var credential = GoogleCredential.FromFile(_firebaseOptions.CredentialsPath);
                    FirebaseApp.Create(new AppOptions { Credential = credential });
                }
                _firebaseAppInitialized = true;
            }
            catch
            {
                // Credentials invalid or missing; push will be skipped
            }
        }
    }

    private async Task SendPushToUserAsync(Guid userId, string title, string body, string? data, Guid notificationId, CancellationToken cancellationToken)
    {
        EnsureFirebaseApp();
        if (FirebaseApp.DefaultInstance == null)
            return;

        var tokens = await _db.FcmDeviceTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => t.Token)
            .ToListAsync(cancellationToken);
        if (tokens.Count == 0)
            return;

        var dataDict = new Dictionary<string, string>
        {
            ["notificationId"] = notificationId.ToString(),
            ["title"] = title,
            ["body"] = body
        };
        if (!string.IsNullOrWhiteSpace(data))
            dataDict["payload"] = data;

        var message = new MulticastMessage
        {
            Tokens = tokens,
            Notification = new FirebaseAdmin.Messaging.Notification
            {
                Title = title,
                Body = body
            },
            Data = dataDict
        };

        try
        {
            var messaging = FirebaseMessaging.DefaultInstance;
            var response = await messaging.SendEachForMulticastAsync(message, cancellationToken);
            if (response.FailureCount > 0)
            {
                var invalidTokens = new List<string>();
                for (var i = 0; i < response.Responses.Count; i++)
                {
                    if (!response.Responses[i].IsSuccess)
                        invalidTokens.Add(tokens[i]);
                }
                if (invalidTokens.Count > 0)
                {
                    var toRemove = await _db.FcmDeviceTokens
                        .Where(t => t.UserId == userId && invalidTokens.Contains(t.Token))
                        .ToListAsync(cancellationToken);
                    _db.FcmDeviceTokens.RemoveRange(toRemove);
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
        }
        catch
        {
            // Log and skip; in-app notification is already saved
        }
    }
}
