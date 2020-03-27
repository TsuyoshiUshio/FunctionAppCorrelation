using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace FunctionAppCorrelation
{
    public class HttpEndpoints
    {

        [FunctionName("CheckSiteAvailable")]
        public async Task<string> CheckSiteAvailable(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            Uri url = context.GetInput<Uri>();

            // Makes an HTTP GET request to the specified endpoint
            DurableHttpResponse response =
                await context.CallHttpAsync(HttpMethod.Get, url);
 
            if ((int)response.StatusCode >= 400)
            {
                // handling of error codes goes here
            }

            return response.Content;
        }

        public class HealthCheck
        {
            public string Status { get; set; }
        }

        [FunctionName("HttpEndpoint")]
        public async Task<IActionResult> HttpEndpoint(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
            HttpRequest req,
            ILogger log)
        {

            log.LogInformation($"HttpEndpoint is called.'.");
            return new OkObjectResult(new HealthCheck() { Status = "Healthy"});
        }

        [FunctionName("HttpStart_HttpEndpoints")]
        public async Task<IActionResult> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId =
                await starter.StartNewAsync("CheckSiteAvailable", new Uri("http://localhost:7071/api/HttpEndpoint"));
            log.LogInformation($"Started HttpEndpoints orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

    }
}
