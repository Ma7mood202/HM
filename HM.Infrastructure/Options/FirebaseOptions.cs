namespace HM.Infrastructure.Options;

/// <summary>
/// Firebase (FCM) credentials. Replace with real values or use CredentialsPath to a service account JSON file.
/// </summary>
public class FirebaseOptions
{
    public const string SectionName = "Firebase";

    /// <summary>Firebase project ID (e.g. from Firebase console).</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Absolute or relative path to the service account JSON key file (download from Firebase Project settings → Service accounts).</summary>
    public string CredentialsPath { get; set; } = string.Empty;
}
