using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ECK1.Integration.Common;
using System.Reflection;

namespace ECK1.Integration.Plugin.Abstractions;

public interface IIntergationPluginRegistry
{
    IIntergationPluginLoader LoadPlugin(IServiceCollection services, IConfiguration config, ProxyConfig proxyConfig, IntegrationConfig integrationConfig);
}

public class IntergationPluginRegistry(ILogger<IntergationPluginRegistry> logger) : IIntergationPluginRegistry
{
    private readonly static Dictionary<string, string> plugins = new(StringComparer.OrdinalIgnoreCase)
    {
#if DEBUG
        ["Elasticsearch"] = "ECK1.Integration.Plugin.ElasticSearch/bin/Debug/net8.0/ECK1.Integration.Plugin.ElasticSearch.dll",
        ["Clickhouse"] = "ECK1.Integration.Plugin.Clickhouse/bin/Debug/net8.0/ECK1.Integration.Plugin.Clickhouse.dll",
#else
        ["Elasticsearch"] = "ElasticSearch/ECK1.Integration.Plugin.ElasticSearch.dll",
        ["Clickhouse"] = "Clickhouse/ECK1.Integration.Plugin.Clickhouse.dll",
#endif
    };

    public IIntergationPluginLoader LoadPlugin(
        IServiceCollection services,
        IConfiguration config,
        ProxyConfig proxyConfig,
        IntegrationConfig integrationConfig)
    {
        Assembly assembly = LoadFromFile(proxyConfig.PluginsDir, proxyConfig.Plugin);

        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IIntergationPluginLoader).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            ?? throw new InvalidOperationException($"No valid {nameof(IIntergationPluginLoader)} implementation in {proxyConfig.Plugin} dll");

        var plugin = (IIntergationPluginLoader)Activator.CreateInstance(pluginType)!;
        plugin.Setup(services, config, integrationConfig);
        return plugin;
    }

    private Assembly LoadFromFile(string pluginDir, string pluginName)
    {
        if (string.IsNullOrEmpty(pluginName) || !plugins.TryGetValue(pluginName, out string pluginPath))
            throw new ArgumentException($"Unknown plugin: '{pluginName}'");

        // /mnt/plugins/ElasticSearch/ECK1.Integration.Plugin.ElasticSearch.dll
        var fullPath = Path.Combine(pluginDir, pluginPath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Plugin not found: {fullPath}");

        // /tmp/plugins/ElasticSearch
        var temp = Path.Combine(Path.GetTempPath(), $"plugins/{pluginName}");

        // /mnt/plugins/ElasticSearch
        var pluginDirFullPath = Path.GetDirectoryName(fullPath);
        logger.LogInformation("Copying from {pluginDirFullPath} to temp path: {temp}", pluginDirFullPath, temp);

        CopyDirectory(pluginDirFullPath, temp, true);

        // ECK1.Integration.Plugin.ElasticSearch.dll
        var pluginLibName = Path.GetFileName(pluginPath);

        // /tmp/plugins/ElasticSearch/ECK1.Integration.Plugin.ElasticSearch.dll
        var fullTempPath = Path.Combine(temp, pluginLibName);
        logger.LogInformation("FullTempPath: {fullTempPath}", fullTempPath);

        return Assembly.LoadFrom(fullTempPath);
    }

    public void CopyDirectory(string sourceDir, string destDir, bool recursive)
    {
        //logger.LogInformation("Wait...");
        //Task.Delay(60000).GetAwaiter().GetResult();
        //logger.LogInformation("Wait completed");
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            try
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                logger.LogInformation("Copy file: {src} to {dst}", file, destFile);
                File.Copy(file, destFile, overwrite: true);
                logger.LogInformation("Copied");
            }
            catch
            {
                logger.LogInformation("Error");
            }
        }

        if (recursive)
        {
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir, true);
            }
        }
    }

    public void CopyFileNoLock(string source, string dest, bool overwrite = true)
    {
        logger.LogInformation("sourceStream");
        using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        logger.LogInformation("destStream");
        using var destStream = new FileStream(dest, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
        logger.LogInformation("sourceStream.CopyTo");
        sourceStream.CopyTo(destStream);
    }
}
