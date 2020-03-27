using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace FunctionAppCorrelation
{
    public class FanOutFanIn
    {
        [FunctionName("FanOutFanInOrchestrator")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var tasks = new Task<string>[3];
            tasks[0] = context.CallActivityAsync<string>("FunOutFunIn_Hello", "Tokyo");
            tasks[1] = context.CallActivityAsync<string>("FunOutFunIn_Hello", "Seattle");
            tasks[2] = context.CallActivityAsync<string>("FunOutFunIn_Hello", "London");
            await Task.WhenAll(tasks);
            var outputs = tasks.Select(p => p.Result).ToList();
            return outputs;
        }

        [FunctionName("FunOutFunIn_Hello")]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name} at the same time.");
            return $"Hello {name}!";
        }

        [FunctionName("HttpStart_FanOutFanIn")]
        public async Task<IActionResult> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
            HttpRequest req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {

            string instanceId = await starter.StartNewAsync("FanOutFanInOrchestrator", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
