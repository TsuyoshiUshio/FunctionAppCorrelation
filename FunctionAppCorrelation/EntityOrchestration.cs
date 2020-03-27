using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FunctionAppCorrelation
{
    /// <summary>
    /// This example is testing for Entity works on the new correlation implementation.
    /// Distributed Tracing for Entity will be next release. 
    /// </summary>
    public class EntityOrchestration
    {
        [FunctionName("QueryCounter")]
        public async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function)] HttpRequestMessage req,
            [DurableClient] IDurableEntityClient client)
        {
            var entityId = new EntityId(nameof(Counter), "myCounter");
            EntityStateResponse<JObject> stateResponse = await client.ReadEntityStateAsync<JObject>(entityId);
            
            await client.SignalEntityAsync(entityId, "Add", 1);
            return req.CreateResponse(HttpStatusCode.OK, stateResponse.EntityState);
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Counter
    {
        [JsonProperty("value")]
        public int CurrentValue { get; set; }

        public void Add(int amount) => this.CurrentValue += amount;

        public void Reset() => this.CurrentValue = 0;

        public int Get() => this.CurrentValue;

        [FunctionName(nameof(Counter))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx)
            => ctx.DispatchAsync<Counter>();
    }
}
