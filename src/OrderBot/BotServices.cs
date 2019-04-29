using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Configuration;
using OrderBot.services;
using System;
using System.Collections.Generic;

namespace OrderBot
{
    /// <summary>
    /// Manages external services for the bot.
    /// </summary>
    public class BotServices
    {
        public BotServices(BotConfiguration botConfiguration)
        {
            foreach (var service in botConfiguration.Services)
            {
                switch (service.Type)
                {
                    case ServiceTypes.Luis:
                        {
                            var luis = (LuisService)service ?? throw new InvalidOperationException("The LUIS service is not configured correctly in your '.bot' file.");
                            var endpoint = (luis.Region?.StartsWith("https://") ?? false) ? luis.Region : luis.GetEndpoint();
                            var app = new LuisApplication(luis.AppId, luis.AuthoringKey, endpoint);
                            var recognizer = new LuisRecognizer(app);

                            LuisServices.Add(luis.Name, recognizer);
                            break;
                        }
                }
            }
        }

        public Dictionary<string, LuisRecognizer> LuisServices { get; } = new Dictionary<string, LuisRecognizer>();

        public IProductsService ProductsService { get; } = new MockProductsService();
    }
}
