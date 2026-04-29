namespace HM.Domain.Enums;

public enum ShipmentStatus
{
    AwaitingDriver,
    PendingDriverAcceptance, // Driver assigned by truck account; awaiting driver's explicit accept (15 min timeout)
    Ready,
    InTransit,
    Arrived,    // Driver marked "تم الوصول" (after Start, before Received)
    Paused,
    Completed,
    Cancelled
}
