using System.Threading.Channels;
using Microsoft.Extensions.Options;
using ResinArt.Api.Infrastructure.Settings;

namespace ResinArt.Api.Infrastructure.Notifications;

public sealed record LeadNotificationEvent(
    string Kind,
    string Reference,
    string CustomerName,
    string CustomerPhone,
    string CustomerEmail,
    string SelectedRegion,
    string? ProductName,
    string? SummaryText,
    DateTimeOffset CreatedAt);

public interface ILeadNotificationQueue
{
    void Enqueue(LeadNotificationEvent notification);
}

/// <summary>
/// Non-blocking in-memory notification queue (<see cref="Channel{T}"/>) processed by a <see cref="BackgroundService"/>.
/// Ensures slow SMTP / Telegram webhooks never delay the customer's instant WhatsApp redirect (H4 in architecture review).
/// </summary>
public sealed class LeadNotificationService : BackgroundService, ILeadNotificationQueue
{
    private readonly Channel<LeadNotificationEvent> _channel;
    private readonly IOptionsMonitor<NotificationOptions> _options;
    private readonly ILogger<LeadNotificationService> _logger;
    private readonly Queue<LeadNotificationEvent> _recent = new();
    private readonly Lock _gate = new();

    public LeadNotificationService(
        IOptionsMonitor<NotificationOptions> options,
        ILogger<LeadNotificationService> logger)
    {
        _options = options;
        _logger = logger;
        var capacity = Math.Max(16, options.CurrentValue.QueueCapacity);
        _channel = Channel.CreateBounded<LeadNotificationEvent>(
            new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.DropOldest });
    }

    public void Enqueue(LeadNotificationEvent notification)
    {
        _channel.Writer.TryWrite(notification);
        var maxHistory = Math.Max(10, _options.CurrentValue.MaxRecentHistory);
        lock (_gate)
        {
            _recent.Enqueue(notification);
            while (_recent.Count > maxHistory)
                _recent.Dequeue();
        }
    }

    public IReadOnlyList<LeadNotificationEvent> GetRecent()
    {
        lock (_gate) return _recent.ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation(
                    "[Lead Notification] {Kind} {Ref} from {Name} ({Phone}, region {Region}) Product={Product} {Summary}",
                    evt.Kind,
                    evt.Reference,
                    evt.CustomerName,
                    evt.CustomerPhone,
                    evt.SelectedRegion,
                    evt.ProductName ?? "-",
                    evt.SummaryText ?? "");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispatch notification for lead {Ref}", evt.Reference);
            }
        }
    }
}
