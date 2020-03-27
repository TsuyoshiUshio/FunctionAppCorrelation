using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace FunctionAppCorrelation
{
    public class CorrelationIntegration
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly HttpClient _httpClient;
        public CorrelationIntegration(TelemetryConfiguration telemetryConfiguration, HttpClient client)
        {
            _telemetryClient = new TelemetryClient(telemetryConfiguration);
            _httpClient = client;
        }
        [FunctionName("Orchestration_W3C")]
        public async Task<List<string>> RunOrchestrator(
           [OrchestrationTrigger] IDurableOrchestrationContext context)
        {

            var correlationContext = CorrelationTraceContext.Current as W3CTraceContext;
            var trace = new TraceTelemetry(
                $"Activity Id: {correlationContext?.TraceParent} ParentSpanId: {correlationContext?.ParentSpanId}");
            trace.Context.Operation.Id = correlationContext?.TelemetryContextOperationId;
            trace.Context.Operation.ParentId = correlationContext?.TelemetryContextOperationParentId;
            _telemetryClient.Track(trace);

            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName("Hello_W3C")]
        public string SayHello([ActivityTrigger] string name, ILogger log)
        {
            // Send Custom Telemetry
            var currentActivity = Activity.Current;
            _telemetryClient.TrackTrace($"Message from Activity: {name}.");

            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName("HttpStart_With_W3C")]
        public async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [DurableClient]IDurableOrchestrationClient starter,
            ILogger log)
        {
            var requestId = req.Headers.GetValues("Request-Id").FirstOrDefault();
            if (string.IsNullOrEmpty(requestId))
            {
                log.LogInformation("Request-Id can not be empty.");
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Request-Id header is required.");
            }

            string instanceId = await starter.StartNewAsync("Orchestration_W3C", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("AnExternalSystem")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
            HttpRequest req,
            ILogger log)
        {
            log.LogInformation($"An external system send a request. ");
            var currentActivity = Activity.Current;
            
            await _httpClient.GetAsync("http://localhost:7071/api/HttpStart_With_W3C");
            return new OkObjectResult("Telemetry Sent.");
        }

    }
}
