namespace UnifiProtectClient.Application.Options;

public sealed class UnifiProtectOptions
{
    public const string SectionName = "UnifiProtect";

    public required string BaseUrl { get; init; }
    public required string ApiKey  { get; init; }
}
