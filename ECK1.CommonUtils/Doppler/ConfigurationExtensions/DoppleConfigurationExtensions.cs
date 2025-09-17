using System.Net.Http.Headers;
using System.Text.Json;

namespace Microsoft.Extensions.Configuration;

public static class DopplerConfigurationExtensions
{
    public static IConfigurationBuilder AddDopplerSecrets(
        this IConfigurationBuilder configuration)
    {
        var tempConfig = configuration.Build();

        var token = tempConfig["Doppler:Token"];
        var projects = tempConfig["Doppler:Projects"]
               ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               ?? Array.Empty<string>();
        var config = tempConfig["Doppler:Config"];
        var apiHost = tempConfig["Doppler:ApiHost"] ?? "https://api.doppler.com";

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Doppler:Token must be provided in configuration.");
        if (projects.Length == 0)
            throw new InvalidOperationException("Doppler:Project must be provided in configuration.");
        if (string.IsNullOrWhiteSpace(config))
            throw new InvalidOperationException("Doppler:Config must be provided in configuration.");

        return configuration.AddDopplerSecrets(token!, apiHost!, projects!, config!);
    }

    public static IConfigurationBuilder AddDopplerSecrets(
        this IConfigurationBuilder configuration,
        string token,
        string apiHost,
        string[] projects,
        string config)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        foreach (var project in projects)
        {
            var url = $"{apiHost}/v3/configs/config/secrets/download?project={project}&config={config}&format=json";
            var response = client.GetStringAsync(url).GetAwaiter().GetResult();

            var dopplerSecrets =
                JsonSerializer.Deserialize<Dictionary<string, string>>(response)
                ?? new Dictionary<string, string>();

            var transformed = dopplerSecrets.ToDictionary(
                            kv => kv.Key.Replace("__", ":"), // __ → :
                            kv => kv.Value);
            configuration.AddInMemoryCollection(transformed);
        }

        return configuration;
    }
}
