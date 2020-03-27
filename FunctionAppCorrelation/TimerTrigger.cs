using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace FunctionAppCorrelation
{
    public class Job
    {
        public string JobId { get; set; }
        public DateTime ExpiryTime { get; set; }
    }
    public class TimerTrigger
    {
        private readonly TelemetryClient _telemetryClient;
        public TimerTrigger(TelemetryConfiguration telemetryConfiguration, HttpClient client)
        {
            _telemetryClient = new TelemetryClient(telemetryConfiguration);
        }

        [FunctionName("MonitorJobStatus")]
        public async Task Run(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var job = context.GetInput<Job>();
            int pollingInterval = 30;   // Second
            while (context.CurrentUtcDateTime < job.ExpiryTime)
            {
                var jobStatus = await context.CallActivityAsync<string>("GetJobStatus", job.JobId);
                if (jobStatus == "Completed")
                {
                    await context.CallActivityAsync("SendAlert", $"Job({job.JobId}) Completed.");
                    break;
                }
                var nextCheck = context.CurrentUtcDateTime.AddSeconds(pollingInterval);
                await context.CreateTimer(nextCheck, CancellationToken.None);
            }
        }

        [FunctionName("SendAlert")]
        public void SendAlert(
            [ActivityTrigger] string message)
        {
            _telemetryClient.TrackTrace(message);
        }

        private static readonly ConcurrentDictionary<string, string> state = new ConcurrentDictionary<string, string>();

        [FunctionName("GetJobStatus")]
        public Task<string> GetJobStatus(
            [ActivityTrigger] IDurableActivityContext context)
        {
            var jobId = context.GetInput<string>();
            var status = state.AddOrUpdate(jobId, "Scheduled", (key, oldValue) =>
            {
                switch (oldValue)
                {
                    case "Scheduled":
                        return "Running";
                    case "Running":
                        return "Completed";
                    default:
                        return "Failed";
                }
            });
            return Task.FromResult(status);
        }

        [FunctionName("HttpStart_Monitor")]
        public async Task<IActionResult> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string jobId = req.Query["JobId"];

            if (string.IsNullOrEmpty(jobId))
            {
                return new BadRequestObjectResult("Parameter JobId can not be null. Add '?JobId={someId}' on your request URI.");
            }

            var expirTime = DateTime.UtcNow.AddSeconds(180);

            string instanceId = await starter.StartNewAsync("MonitorJobStatus", new Job()
            {
                JobId = jobId,
                ExpiryTime = expirTime
            });

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

    }
}
