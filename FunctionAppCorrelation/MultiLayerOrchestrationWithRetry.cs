using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DurableTask.Core;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Correlation;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using RetryOptions = Microsoft.Azure.WebJobs.Extensions.DurableTask.RetryOptions;

namespace FunctionAppCorrelation
{
    public static class MultiLayerOrchestrationWithRetry
    {
        [FunctionName("MultiLayerOrchestrationWithRetry")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            var child1Result = await context.CallSubOrchestratorAsync<List<string>>("ChildOrchestration", null);
            var child2Result = await context.CallSubOrchestratorAsync<List<string>>("ChildOrchestration", null);
            outputs.AddRange(child1Result);
            outputs.AddRange(child2Result);
            return outputs;
        }

        [FunctionName("ChildOrchestration")]
        public static async Task<List<string>> ChildOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();
            var tasks = new Task<string>[3];
            var option = new RetryOptions(TimeSpan.FromSeconds(5), 3);
            tasks[0] = context.CallActivityWithRetryAsync<string>("MultiLayerOrchestrationWithRetry_Hello", option, "Osaka");
            tasks[1] = context.CallActivityWithRetryAsync<string>("MultiLayerOrchestrationWithRetry_Hello", option, "Seattle");
            tasks[2] = context.CallActivityWithRetryAsync<string>("MultiLayerOrchestrationWithRetry_Hello", option, "Atlanta");
            await Task.WhenAll(tasks);
            return tasks.Select((i) => i.Result).ToList();
        }

        private static int count1 = 0; 
        private static int count2 = 0;
        private static object countLock = new Object(); 
        [FunctionName("MultiLayerOrchestrationWithRetry_Hello")]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            // This is the demo code for retrying happens for the first two execution.
            lock (countLock)
                    {
                        if (count1 == 0)
                {
                    count1++;
                    throw new Exception("Something bad happened.");
                }

                if (count2 == 3)
                {
                    count2++;
                    throw new Exception("Something wrong happened.");
                }

                count1++;
                count2++;
                    }

            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        [FunctionName("MultiLayerOrchestrationWithRetry_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [DurableClient]IDurableOrchestrationClient starter,
            ILogger log)
        {
            string instanceId = await starter.StartNewAsync("MultiLayerOrchestrationWithRetry", null);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
            return starter.CreateCheckStatusResponse(req, instanceId);

        }
    }
}