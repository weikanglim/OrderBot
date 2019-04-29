using OrderBot.models;
using System;
using System.Collections.Generic;

namespace OrderBot.services
{
    public interface IProductsService
    {
        List<Product> ListProducts();

        IList<Product> FindProduct(string searchString);

        Product GetProduct(Guid productId);
    }
}
