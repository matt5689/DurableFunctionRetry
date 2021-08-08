using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;

namespace DurabeTemp
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var retryOptions = new RetryOptions(
                firstRetryInterval: TimeSpan.FromSeconds(1),
                maxNumberOfAttempts: 3);

            var outputs = new List<string>();

            Thread.Sleep(3000);

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityWithRetryAsync<string>("Function1_Hello", retryOptions, "Tokyo"));
            //outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "Seattle"));
            //outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName("TimerOrchestrator")]
        public static async Task<bool> Run([OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            Thread.Sleep(3000);
            var retryOptions = new RetryOptions(
                firstRetryInterval: TimeSpan.FromSeconds(1),
                maxNumberOfAttempts: 3);

            TimeSpan timeout = TimeSpan.FromSeconds(3);
            DateTime deadline = context.CurrentUtcDateTime.Add(timeout);

            using (var cts = new CancellationTokenSource())
            {
                Task activityTask = context.CallActivityWithRetryAsync("Function1_Hello", retryOptions, "Tokyo2");
                Task timeoutTask = context.CreateTimer(deadline, cts.Token);

                Task winner = await Task.WhenAny(activityTask, timeoutTask);
                if (winner == activityTask)
                {
                    // success case
                    log.LogInformation("ORCHESTRATION SUCCESS!!!");
                    cts.Cancel();
                    return true;
                }
                else
                {
                    // timeout case
                    log.LogInformation("ORCHESTRATION FAILURE!!!");
                    return false;
                }
            }
        }

        [FunctionName("Function1_Hello")]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            Thread.Sleep(3000);
            log.LogInformation($"Saying hello to {name}.");

            //Thread.CurrentThread.Abort();            

            throw new Exception("EXCEPTION!!!");
            return $"Hello {name}!";
        }

        [FunctionName("Function1_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            Thread.Sleep(3000);
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("TimerOrchestrator", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}