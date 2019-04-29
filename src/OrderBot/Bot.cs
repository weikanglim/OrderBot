using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using OrderBot.models;
using OrderBot.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OrderBot
{
    public class Bot : IBot
    {
        private readonly ILogger _logger;
        private readonly BotServices _services;
        private readonly ConversationState _conversationState;

        public static readonly string LuisConfiguration = "BotLuisApplication";


        private readonly IStatePropertyAccessor<DialogState> _dialogStateAccessor;
        private readonly IStatePropertyAccessor<Order> _OrderAccessor;


        private DialogSet Dialogs { get; set; }

        private LuisRecognizer LuisRecognizer { get { return _services.LuisServices[LuisConfiguration]; } }

        public const string OrderIntent = "Order";
        public const string ProductsIntent = "Products";
        public const string NoneIntent = "None";


        public Bot(BotServices services, ConversationState conversationState, ILoggerFactory loggerFactory)
        {
            _conversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));
            _services = services ?? throw new ArgumentNullException(nameof(services));

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<ILogger<Bot>>();
            _OrderAccessor = conversationState.CreateProperty<Order>(nameof(Order));
            _dialogStateAccessor = conversationState.CreateProperty<DialogState>(nameof(DialogState));

            // Verify LUIS configuration.
            if (!_services.LuisServices.ContainsKey(LuisConfiguration))
            {
                throw new InvalidOperationException($"The bot configuration does not contain a service type of `luis` with the id `{LuisConfiguration}`.");
            }

            Dialogs = new DialogSet(_dialogStateAccessor);
            Dialogs.Add(new OrderDialog(_OrderAccessor, services.ProductsService, loggerFactory));
            Dialogs.Add(new ProductQueryDialog(services.ProductsService, loggerFactory));
        }

        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            var activity = turnContext.Activity;
            var dialog = await Dialogs.CreateContextAsync(turnContext);

            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                string utterance = turnContext.Activity.Text.Trim().ToLowerInvariant();

                switch (utterance)
                {
                    case "order":
                        await dialog.BeginDialogAsync(nameof(OrderDialog));
                        await _conversationState.SaveChangesAsync(turnContext);
                        return;
                }

                var luisResults = await LuisRecognizer.RecognizeAsync(dialog.Context, cancellationToken);
                var topScoringIntent = luisResults?.GetTopScoringIntent();
                var topIntent = topScoringIntent.Value.intent;
                await UpdateState(luisResults, turnContext);

                var dialogResult = await dialog.ContinueDialogAsync();

                if (!dialog.Context.Responded)
                {
                    // examine results from active dialog
                    switch (dialogResult.Status)
                    {
                        case DialogTurnStatus.Empty: 
                            switch (topIntent)
                            {
                                case OrderIntent:
                                    await dialog.BeginDialogAsync(nameof(OrderDialog));
                                    break;

                                case ProductsIntent:
                                    var parseProductName = (string) luisResults.Entities["product"][0];
                                    await dialog.BeginDialogAsync(nameof(ProductQueryDialog), new ProductQuery() { ProductName = parseProductName });
                                    break;

                                case NoneIntent:
                                    await SendHelpMessage(dialog);
                                    break;
                            }
                            
                            break;

                        case DialogTurnStatus.Waiting:
                            // The active dialog is waiting for a response from the user, so do nothing.
                            break;

                        case DialogTurnStatus.Complete:
                            await dialog.EndDialogAsync();
                            break;

                        default:
                            await dialog.CancelAllDialogsAsync();
                            break;
                    }
                }
            }
            else if (activity.Type == ActivityTypes.ConversationUpdate)
            {
                if (activity.MembersAdded != null)
                {
                    foreach (var member in activity.MembersAdded)
                    {
                        if (member.Id != activity.Recipient.Id)
                        {
                            await SendWelcomeMessage(dialog, member);
                        }
                    }
                }
            }

           await _conversationState.SaveChangesAsync(turnContext);
        }

        private async Task SendWelcomeMessage(DialogContext dialog, ChannelAccount account)
        {
            var reply = dialog.Context.Activity.CreateReply($"Welcome, {account.Name}. I can help you with the following things:");
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction() { Title = "Order", Type = ActionTypes.ImBack, Value = "Order" },
                    new CardAction() { Title = "Ask for the price of an item", Type = ActionTypes.ImBack, Value = "What is the price of a cheeseburger?" },
                }
            };

            await dialog.Context.SendActivityAsync(reply);
        }

        private async Task SendHelpMessage(DialogContext dialog)
        {
            var reply = dialog.Context.Activity.CreateReply($"I'm sorry. I didn't quite understand that. Here are the list of things I can help you with:");
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction() { Title = "Order", Type = ActionTypes.ImBack, Value = "Order" },
                    new CardAction() { Title = "Ask for the price of an item", Type = ActionTypes.ImBack, Value = "What is the price of a cheeseburger?" },
                }
            };

            await dialog.Context.SendActivityAsync(reply);
        }


        private async Task DisplayProductsListMessage(DialogContext dialog)
        {
            var reply = dialog.Context.Activity.CreateReply("Here are the products available:");
            reply.SuggestedActions = new SuggestedActions()
            {
                Actions = _services.ProductsService
                    .ListProducts()
                    .Select(x => new CardAction() {
                        Title = $"{x.Name} : ${x.Price}",
                        Type = ActionTypes.ImBack,
                        Value = $"Order {x.Name}" })
                    .ToList()
            };

            await dialog.Context.SendActivityAsync(reply);
        }


        private async Task UpdateState(RecognizerResult luisResult, ITurnContext turnContext)
        {
            if (luisResult.Entities != null && luisResult.Entities.HasValues)
            {
                var orderState = await _OrderAccessor.GetAsync(turnContext, () => new Order());
                var entities = luisResult.Entities;

                string[] orderEntities = { "product"};

                if (luisResult.GetTopScoringIntent().intent == OrderIntent)
                {
                    foreach (var name in orderEntities)
                    {
                        if (entities[name] != null)
                        {
                            orderState.ItemToAdd = (string)entities[name][0];
                            break;
                        }
                    }
                }

                // Set the new values into state.
                await _OrderAccessor.SetAsync(turnContext, orderState);
            }
        }
    }
}
