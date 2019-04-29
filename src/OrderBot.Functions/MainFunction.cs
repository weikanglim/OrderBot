using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Builder.Integration.AspNet.Core.Handlers;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Willezone.Azure.WebJobs.Extensions.DependencyInjection;

namespace OrderBot
{
    public static class MainFunction
    {
        [FunctionName("messages")]
        public static async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)]HttpRequest req,
            [Inject] IAdapterIntegration adapter,
            [Inject] IBot bot,
            ILogger logger)
        {
            try
            {
                await ProcessMessageRequestAsync(req, adapter, bot.OnTurnAsync, default(CancellationToken));

                return new OkResult();
            }
            catch (Exception ex)
            {
                return new ObjectResult(ex) { StatusCode = StatusCodes.Status500InternalServerError };
            }
        }

        private static async Task<InvokeResponse> ProcessMessageRequestAsync(HttpRequest request, IAdapterIntegration adapter, BotCallbackHandler botCallbackHandler, CancellationToken cancellationToken)
        {
            var requestBody = request.Body;

            // In the case of request buffering being enabled, we could possibly receive a Stream that needs its position reset,
            // but we can't _blindly_ reset in case buffering hasn't been enabled since we'll be working with a non-seekable Stream
            // in that case which will throw a NotSupportedException
            if (requestBody.CanSeek)
            {
                requestBody.Position = 0;
            }

            var activity = default(Activity);

            // Get the request body and deserialize to the Activity object.
            // NOTE: We explicitly leave the stream open here so others can still access it (in case buffering was enabled); ASP.NET runtime will always dispose of it anyway
            using (var bodyReader = new JsonTextReader(new StreamReader(requestBody, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true)))
            {
                activity = BotMessageHandlerBase.BotMessageSerializer.Deserialize<Activity>(bodyReader);
            }

            var invokeResponse = await adapter.ProcessActivityAsync(
                    request.Headers["Authorization"],
                    activity,
                    botCallbackHandler,
                    cancellationToken);

            return invokeResponse;
        }
    }
}
