using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Reflection;

namespace ECK1.CommonUtils.OpenTelemetry;

public static class OpenTelemetryExtensions
{
    private static readonly string[] DefaultIgnoredExtensions =
    [
        ".css", ".js", ".png", ".jpg", ".jpeg", ".svg", ".ico", ".map", ".woff", ".woff2", ".ttf", ".eot", ".gif", ".html", ".htm"
    ];

    /// <summary>
    /// Configures OpenTelemetry logs, traces and metrics using OTEL_* environment variables.
    /// Expected variables:
    /// - OTEL_SERVICE_NAME, OTEL_SERVICE_VERSION
    /// - OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_PROTOCOL ("grpc" | "http/protobuf")
    /// </summary>
    public static WebApplicationBuilder AddOpenTelemetry(
        this WebApplicationBuilder builder,
        string[] ignoredRequestPathPrefixes = null,
        string[] ignoredStaticFileExtensions = null,
        Action<TracerProviderBuilder> tracingExtraConfig = null)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        ignoredRequestPathPrefixes ??= ["/swagger", "/health"]; 
        ignoredStaticFileExtensions ??= DefaultIgnoredExtensions;

        var configuration = builder.Configuration;

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
        Sdk.SetDefaultTextMapPropagator(
            new CompositeTextMapPropagator(
                new TextMapPropagator[]
                {
                    new TraceContextPropagator(),
                    new BaggagePropagator(),
                })
        );

        var assemblyName = Assembly.GetEntryAssembly()?.GetName() ?? Assembly.GetExecutingAssembly().GetName();

        var serviceName = configuration["OTEL_SERVICE_NAME"] ?? assemblyName.Name;
        var serviceVersion = configuration["OTEL_SERVICE_VERSION"] ?? assemblyName.Version.ToString();

        Uri otlpEndpoint = null;
        var otlpEndpointRaw = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(otlpEndpointRaw) && Uri.TryCreate(otlpEndpointRaw, UriKind.Absolute, out var parsedEndpoint))
        {
            otlpEndpoint = parsedEndpoint;
        }

        OtlpExportProtocol? otlpProtocol = null;
        var otlpProtocolRaw = configuration["OTEL_EXPORTER_OTLP_PROTOCOL"];
        if (!string.IsNullOrWhiteSpace(otlpProtocolRaw))
        {
            if (string.Equals(otlpProtocolRaw, "grpc", StringComparison.OrdinalIgnoreCase))
            {
                otlpProtocol = OtlpExportProtocol.Grpc;
            }
            else if (string.Equals(otlpProtocolRaw, "http/protobuf", StringComparison.OrdinalIgnoreCase))
            {
                otlpProtocol = OtlpExportProtocol.HttpProtobuf;
            }
        }

        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(
                serviceName: serviceName,
                serviceVersion: serviceVersion,
                serviceInstanceId: Environment.MachineName);

        void ConfigureOtlpExporter(OtlpExporterOptions otlp)
        {
            if (otlpEndpoint is not null)
            {
                otlp.Endpoint = otlpEndpoint;
            }

            if (otlpProtocol is not null)
            {
                otlp.Protocol = otlpProtocol.Value;
            }
        }

        // Ensure conventional (stdout) logs can be correlated with traces.
        // This makes ILogger providers (Console, etc.) include TraceId/SpanId in the logging scope.
        builder.Logging.Configure(options =>
        {
            options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId;
        });

        // Make sure console formatters actually print scopes (where TraceId/SpanId live).
        // This affects logs scraped from stdout (e.g., via Promtail/Loki).
        builder.Services.Configure<SimpleConsoleFormatterOptions>(options =>
        {
            options.IncludeScopes = true;
        });

        builder.Services.Configure<JsonConsoleFormatterOptions>(options =>
        {
            options.IncludeScopes = true;
        });

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;

            options.AddOtlpExporter(ConfigureOtlpExporter);
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: serviceName,
                serviceVersion: serviceVersion,
                serviceInstanceId: Environment.MachineName))
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = context =>
                        {
                            var path = context.Request.Path.Value ?? string.Empty;

                            foreach (var prefix in ignoredRequestPathPrefixes)
                            {
                                if (context.Request.Path.StartsWithSegments(prefix))
                                {
                                    return false;
                                }
                            }

                            var ext = Path.GetExtension(path);
                            if (!string.IsNullOrEmpty(ext) && ignoredStaticFileExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                            {
                                return false;
                            }

                            return true;
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddOtlpExporter(ConfigureOtlpExporter);

                tracingExtraConfig?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddOtlpExporter(ConfigureOtlpExporter);
            });

        return builder;
    }
}
