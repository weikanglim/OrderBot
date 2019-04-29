using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Extensions.Logging;
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
    class ProductQueryDialog : ComponentDialog
    {
        private IProductsService ProductsService { get; }

        public ProductQueryDialog(IProductsService productsService, ILoggerFactory loggerFactory)
            : base(nameof(ProductQueryDialog))
        {
            ProductsService = productsService;

            // Add control flow dialogs
            var waterfallSteps = new WaterfallStep[]
            {
                RespondToQueryAsync
            };

            AddDialog(new WaterfallDialog("ProductQueryDialog", waterfallSteps));
        }

        public async Task<DialogTurnResult> RespondToQueryAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var query = stepContext.Options as ProductQuery;
            var product = ProductsService.FindProduct(query.ProductName).FirstOrDefault();
            var context = stepContext.Context;

            if (product == null)
            {
                await context.SendActivityAsync(MessageFactory.Text($"I'm sorry. {query.ProductName} does not match an existing item."));
            }
            else
            {
                await context.SendActivityAsync(MessageFactory.Text($"The price of {query.ProductName} is ${product.Price:0.00}."));
            }

            return await stepContext.EndDialogAsync(null, cancellationToken);
        }
    }
}
