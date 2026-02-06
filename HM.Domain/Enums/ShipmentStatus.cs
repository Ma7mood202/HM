namespace HM.Domain.Enums;

public enum ShipmentStatus
{
    AwaitingDriver,
    Ready,
    InTransit,
    Arrived,    // Driver marked "تم الوصول" (after Start, before Received)
    Paused,
    Completed,
    Cancelled
}
