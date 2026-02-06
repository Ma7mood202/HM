namespace HM.Domain.Enums;

public enum ShipmentRequestStatus
{
    Draft,
    Open,           // PendingOffers – available for offers
    OfferAccepted,
    InProgress,
    Cancelled,
    Expired,
    Completed
}
