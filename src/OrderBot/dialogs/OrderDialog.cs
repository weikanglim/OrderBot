using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Extensions.Logging;
using OrderBot.Dialogs;
using OrderBot.models;
using OrderBot.services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrderBot.Dialogs
{
    class OrderDialog : ComponentDialog
    {
        // Prompts names
        private const string OrderPrompt = "orderPrompt";
        private const string OrderConfirmPrompt = "orderConfirmPrompt";

        public IStatePropertyAccessor<Order> OrderStateAccessor { get; }
        private IProductsService ProductsService { get; }

        private const string OrderDialogId = "orderDialog";

        public static List<string> Interrupts { get; } = new List<string>
        {
            "More info",
            "Process order",
            "Help",
            "Cancel",
        };

        public OrderDialog(IStatePropertyAccessor<Order> orderStateAccessor, IProductsService productsService, ILoggerFactory loggerFactory)
            : base(nameof(OrderDialog))
        {
            OrderStateAccessor = orderStateAccessor ?? throw new ArgumentNullException(nameof(orderStateAccessor));
            ProductsService = productsService;

            // Add control flow dialogs
            var waterfallSteps = new WaterfallStep[]
            {
                    InitializeStateStepAsync,
                    PromptForOrderStepAsync,
                    ProcessInputAsync,
                    ConfirmFinalOrderAsync,
                    ProcessFinalOrderAsync,
            };

            AddDialog(new WaterfallDialog(OrderDialogId, waterfallSteps));
            AddDialog(new ChoicePrompt(OrderPrompt));
            AddDialog(new ChoicePrompt(OrderConfirmPrompt));
        }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var orderState = await OrderStateAccessor.GetAsync(stepContext.Context, () => null);
            if (orderState == null)
            {
                var orderStateOpt = stepContext.Options as Order;
                if (orderStateOpt != null)
                {
                    await OrderStateAccessor.SetAsync(stepContext.Context, orderStateOpt);
                }
                else
                {
                    await OrderStateAccessor.SetAsync(stepContext.Context, new Order());
                }
            }

            return await stepContext.NextAsync();
        }
        private async Task<DialogTurnResult> PromptForOrderStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var context = stepContext.Context;
            var orderState = await OrderStateAccessor.GetAsync(context);

            if (!string.IsNullOrWhiteSpace(orderState.ItemToAdd))
            {
                string itemToAdd = orderState.ItemToAdd;

                // Pop the item to process
                orderState.ItemToAdd = null;
                await OrderStateAccessor.SetAsync(context, orderState);

                // Skip to next step
                return await stepContext.NextAsync(new FoundChoice() { Value = itemToAdd });
            }

            bool cartHasItemsAddd = orderState.Products?.Count > 0;
            var promptText = cartHasItemsAddd ? "Would you like to add something else?" : "What would you like to order?";
            var choices = ChoiceFactory.ToChoices(ProductsService.ListProducts().Select(x => x.Name).Concat(Interrupts).ToList());

            if (cartHasItemsAddd)
            {
                var processOrderChoice = choices.Where(x => x.Value.Equals("process order", StringComparison.OrdinalIgnoreCase)).First();
                choices.Remove(processOrderChoice);
                choices.Insert(0, processOrderChoice);
            }

            var opts = new PromptOptions
            {
                Prompt = MessageFactory.Text(promptText),
                RetryPrompt = MessageFactory.Text("I'm sorry, I didn't understand that. What would you like to order?"),
                Choices = choices,
            };

            return await stepContext.PromptAsync(OrderPrompt, opts);
        }

        /// <summary>
        /// Defines the second step of the main dialog, which is to process the user's input, and
        /// repeat or exit as appropriate.
        /// </summary>
        /// <param name="stepContext">The current waterfall step context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task to perform.</returns>
        private async Task<DialogTurnResult> ProcessInputAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var context = stepContext.Context;
            // Get the user's choice from the previous prompt.
            string response = (stepContext.Result as FoundChoice).Value;

            Order order = await OrderStateAccessor.GetAsync(stepContext.Context);

            switch (response.ToLower())
            {
                case "process order":
                    return await stepContext.NextAsync(null, cancellationToken);

                case "more info":
                    string message = "More info: \n" + String.Join("\n", ProductsService.ListProducts().Select(x => x.ExtendedDescription()));
                    await stepContext.Context.SendActivityAsync(
                        message,
                        cancellationToken: cancellationToken);

                    return await stepContext.ReplaceDialogAsync(OrderDialogId, null, cancellationToken);

                case "cancel":
                    await stepContext.Context.SendActivityAsync("Your order has been canceled", cancellationToken: cancellationToken);
                    await OrderStateAccessor.SetAsync(stepContext.Context, null);
                    return await stepContext.EndDialogAsync(null, cancellationToken);

                case "help":
                    string help = "To make an order, add as many items to your cart as you like. Choose the `Process order` to check out. "
                        + "Choose `Cancel` to cancel your order and exit.";
                    await stepContext.Context.SendActivityAsync(
                        help,
                        cancellationToken: cancellationToken);

                    return await stepContext.ReplaceDialogAsync(OrderDialogId, null, cancellationToken);
            }

            // We've checked for expected interruptions. Check for a valid item choice.
            var productName = response.Split(':')[0].TrimEnd(' ');
            var productToOrder = ProductsService.FindProduct(productName).FirstOrDefault();
            if (productToOrder == null)
            {
                await stepContext.Context.SendActivityAsync("Sorry, that is not a valid item. " +
                    "Please pick one from the menu.");

                return await stepContext.ReplaceDialogAsync(OrderDialogId, null, cancellationToken);
            }
            else
            {
                // Add the item to cart.
                order.Products.Add(productToOrder);
                order.Total += productToOrder.Price;

                // Acknowledge the input.
                await stepContext.Context.SendActivityAsync(
                    $"Added `{response}` to your order; your total is ${order.Total:0.00}.",
                    cancellationToken: cancellationToken);

                var orderState = await OrderStateAccessor.GetAsync(context);
                if (!string.IsNullOrWhiteSpace(orderState.ItemToAdd))
                {
                    // Pop the item to process
                    orderState.ItemToAdd = null;
                    await OrderStateAccessor.SetAsync(context, orderState);
                }
                
                 return await stepContext.ReplaceDialogAsync(OrderDialogId, null, cancellationToken);
            }
        }


        private async Task<DialogTurnResult> ConfirmFinalOrderAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var context = stepContext.Context;
            var order = await OrderStateAccessor.GetAsync(context);
            var orderConfirmText = "Your final order: \n"
                + string.Join("\n", order.Products.Select(x => x.ToString()))
                + $"\n Total: {order.Total:0.00}"
                + "\n Would you like to proceed?";


            var opts = new PromptOptions
            {
                Prompt = MessageFactory.Text(orderConfirmText),
                RetryPrompt = MessageFactory.Text("I'm sorry, I didn't quite understand that. Would you like to proceed?"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Yes", "No" }),
            };

            return await stepContext.PromptAsync(OrderPrompt, opts);
        }

        private async Task<DialogTurnResult> ProcessFinalOrderAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var context = stepContext.Context;
            var order = await OrderStateAccessor.GetAsync(context);
            string response = (stepContext.Result as FoundChoice).Value;

            if (response.Equals("No", StringComparison.OrdinalIgnoreCase))
            {
                // Restart the dialog to allow the user to add additional items
                return await stepContext.ReplaceDialogAsync(OrderDialogId, null, cancellationToken);
            }

            order.ReadyToProcess = true;
            order.OrderDateTime = DateTime.Now;
            await stepContext.Context.SendActivityAsync("Your order has been placed.", cancellationToken: cancellationToken);

            // Process the order and exit.
            order.OrderProcessed = true;
            await OrderStateAccessor.SetAsync(stepContext.Context, null);

            return await stepContext.EndDialogAsync(null, cancellationToken);
        }
    }
}
