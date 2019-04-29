using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using OrderBot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Connector.Authentication;
using System;

namespace OrderBot.Hosted
{
    public class Startup
    {
        private readonly ILogger<Startup> _logger;

        public Startup(IHostingEnvironment env, IConfiguration configuration, ILogger<Startup> logger)
        {
            _logger = logger;

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            if (env.IsDevelopment())
            {
                builder.AddUserSecrets<Startup>(false);
            }

            Configuration = builder.Build();
        }

        public IConfiguration Configuration { get; }
        
        public void ConfigureServices(IServiceCollection services)
        {
            var secretKey = Configuration["botFileSecret"];
            var botFilePath = Configuration["botFilePath"];
            var storage = new MemoryStorage();

            if (!File.Exists(botFilePath))
            {
                throw new FileNotFoundException($"The .bot configuration file was not found. botFilePath: {botFilePath}");
            }

            BotConfiguration botConfig = BotConfiguration.Load(botFilePath, secretKey);

            services.AddSingleton(sp => botConfig ?? throw new InvalidOperationException($"The .bot configuration file could not be loaded. botFilePath: {botFilePath}"));

            // Add BotServices singleton.
            // Create the connected services from .bot file.
            services.AddSingleton(sp => new BotServices(botConfig));
            services.AddSingleton(new ConversationState(storage));
            services.AddSingleton(new UserState(storage));
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
                    _logger.LogError(ex, "Exception occurred during creation of Bot instance.");
                    return null;
                }
            }, options =>
            {
                var appId = Configuration[@"MS_APP_ID"];
                var pwd = Configuration[@"MS_APP_PASSWORD"];

                options.CredentialProvider = new SimpleCredentialProvider(appId, pwd);
                options.OnTurnError = async (context, exception) =>
                {
                    _logger.LogError(exception, "OnTurnError");
                    await context.SendActivityAsync("Sorry, it looks like something went wrong.");
                };
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseBotFramework();
        }
    }
}
