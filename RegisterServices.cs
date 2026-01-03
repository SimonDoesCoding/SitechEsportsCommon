using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sitech.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sitech.Common;

public static class RegisterServices
{
    public const string CorrelationRequestHeaderId = "X-Correlation-Id";
    public const string BpgRequestIpAddressBaggagePropertyName = "BpgRequestIPAddress";
    public const string CorrelationTagId = "CorrelationId";

    public static IServiceCollection RegisterOtel(this IServiceCollection services)
    {
        services
            .AddOpenTelemetry()
            .UseOtlpExporter()
            .ConfigureResource(resource => resource
                .AddService("sitech-esports"))
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
                .AddSource("sitech-esports")
                .AddProcessor(new CollaborationIdProcessor())
                .SetSampler(new AlwaysOnSampler())
                //.AddOtlpExporter(options =>
                //{
                //    options.Endpoint = new Uri("https://otlp.nr-data.net");
                //    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                //    options.BatchExportProcessorOptions.MaxExportBatchSize = 2048;
                //    options.BatchExportProcessorOptions.MaxQueueSize = 8192;
                //    options.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 250;
                //})
             )
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
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