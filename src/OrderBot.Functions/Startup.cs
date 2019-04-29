using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderBot;
using System;
using System.IO;
using System.Net.Http;
using Willezone.Azure.WebJobs.Extensions.DependencyInjection;

[assembly: WebJobsStartup(typeof(Startup))]
namespace OrderBot
{

    internal class Startup : IWebJobsStartup
    {
        private static HttpClient client = new HttpClient();

        public void Configure(IWebJobsBuilder builder) =>
            builder.AddDependencyInjection(ConfigureServices);

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.AddApplicationInsights(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY"));
            });

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<Startup>>();

            try
            {
                logger.LogInformation("Startup started");

                var secretKey = Environment.GetEnvironmentVariable("botFileSecret");
                var botFilePath = Environment.GetEnvironmentVariable("botFilePath");
                var storage = new CosmosDbStorage(new CosmosDbStorageOptions()
                {
                    AuthKey = Environment.GetEnvironmentVariable("DocumentDbKey"),
                    CosmosDBEndpoint = new Uri(Environment.GetEnvironmentVariable("DocumentDbUrl")),
                    CollectionId = Environment.GetEnvironmentVariable("DocumentDbCollection"),
                    DatabaseId = Environment.GetEnvironmentVariable("DocumentDbDatabase")
                });

                if (!File.Exists(botFilePath))
                {
                    throw new FileNotFoundException($"The .bot configuration file was not found. botFilePath: {botFilePath}");
                }

                logger.LogInformation("Bot configuration found at {botFilePath}.", botFilePath);

                BotConfiguration botConfig = BotConfiguration.Load(botFilePath, secretKey);

                services.AddSingleton(sp => botConfig ?? throw new InvalidOperationException($"The .bot configuration file could not be loaded. botFilePath: {botFilePath}"));

                // Add BotServices singleton.
                // Create the connected services from .bot file.
                services.AddSingleton(sp => new BotServices(botConfig));
                services.AddSingleton(new ConversationState(storage));
                services.AddSingleton(new UserState(storage));
                services.AddSingleton(serviceProvider.GetRequiredService<ILogger<IAdapterIntegration>>());
                services.AddBot(botServiceProvider =>
                {
                    try
                    {
                        return new Bot(
                            botServiceProvider.GetRequiredService<BotServices>(),
                            botServiceProvider.GetRequiredService<ConversationState>(),
                            botServiceProvider.GetRequiredService<ILoggerFactory>());
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Exception occurred during creation of Bot instance.");
                        return null;
                    }
                },
                options =>
                {
                    var appId = Environment.GetEnvironmentVariable(@"MS_APP_ID");
                    var pwd = Environment.GetEnvironmentVariable(@"MS_APP_PASSWORD");

                    options.CredentialProvider = new SimpleCredentialProvider(appId, pwd);
                    options.HttpClient = client;
                    options.OnTurnError = async (context, exception) =>
                    {
                        var botLogger = serviceProvider.GetRequiredService<ILogger<Bot>>();
                        botLogger.LogError(exception, "OnTurnError");
                        await context.SendActivityAsync("Sorry, it looks like something went wrong.");
                    };
                });

                logger.LogInformation("Startup was succesful");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception occurred during startup.");
            }
        }
    }

}