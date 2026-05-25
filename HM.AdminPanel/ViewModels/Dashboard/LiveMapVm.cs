namespace HM.AdminPanel.ViewModels.Dashboard;

public class LiveMapVm
{
    public string  HubUrl   { get; set; } = string.Empty;
    public List<ActiveShipmentPin> Pins { get; set; } = new();
}

public record ActiveShipmentPin(Guid ShipmentId, double? Lat, double? Lng, string Status);
