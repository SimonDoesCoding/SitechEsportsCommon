using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace Sitech.Common;

public static class RegisterServices
{
    public const string CorrelationRequestHeaderId = "X-Correlation-Id";
    public const string CorrelationTagId = "CorrelationId";

    public static IServiceCollection RegisterOtel(this IServiceCollection services, IConfiguration config)
    {
        var otelConfig = config.GetRequiredSection("OtelConfig");

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("sitech-esports"))
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
                .AddSource("sitech-esports")
                .AddProcessor(new CollaborationIdProcessor())
                .SetSampler(new AlwaysOnSampler())
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otelConfig.GetValue<string>("Endpoint") ?? 
                        throw new MissingFieldException("OTEL Endpoint is missing from the config"));
                    options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    options.Headers = otelConfig.GetValue<string>("ApiKey") ?? 
                        throw new MissingFieldException("OTEL ApiKey is missing from the config");
                })
             )
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otelConfig.GetValue<string>("Endpoint") ??
                        throw new MissingFieldException("OTEL Endpoint is missing from the config"));
                    options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    options.Headers = "api-key=3ab70284a36a834dd5d1602e955d3b87FFFFNRAL";
                })
                .AddAspNetCoreInstrumentation((options) =>
                {
                    options.RecordException = true;
                    options.EnrichWithHttpRequest = (activity, httpRequest) =>
                    {
                        if (activity.IsAllDataRequested)
                        {
                            if (httpRequest.HttpContext?.Request.Headers.TryGetValue(CorrelationRequestHeaderId,
                                    out var value) == true
                                && !string.IsNullOrEmpty(value))
                            {
                                Baggage.SetBaggage(CorrelationTagId, value);
                                return;
                            }

                            Baggage.SetBaggage(CorrelationTagId, activity.TraceId.ToString());
                        }
                    };
                }
                ));

        services.AddSingleton(new ActivitySource("sitech-esports"));

        return services;
    }
}

    public class CollaborationIdProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity activity)
    {
        var correlationId = Baggage.GetBaggage(RegisterServices.CorrelationTagId);
        if (!string.IsNullOrEmpty(correlationId))
        {
            activity?.SetTag(RegisterServices.CorrelationTagId, correlationId);
        }
    }
}