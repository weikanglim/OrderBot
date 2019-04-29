using System;
using System.Collections.Generic;
using System.Text;
using OrderBot.models;

namespace OrderBot.services
{
    public class MockProductsService : IProductsService
    {
        public IList<Product> FindProduct(string searchString)
        {
            var products = ListProducts();
            return products.FindAll(x => x.Name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public Product GetProduct(Guid productId)
        {
            return ListProducts().Find(x => x.Id == productId);
        }

        public List<Product> ListProducts()
        {
            return new List<Product>()
            {
                new Product
                {
                    Name = "Hamburger",
                    Description = "Contains 330 calories per serving.",
                    Price = 1.50
                },
                new Product
                {
                    Name = "Cheeseburger",
                    Description = "Contains 400 calories per serving.",
                    Price = 2.50
                },
                new Product
                {
                    Name = "Fries",
                    Description = "Contains 150 calories per serving.",
                    Price = 1.00
                },
                new Product
                {
                    Name = "Drink",
                    Description = "Contains 200 calories per serving.",
                    Price = 1.00
                }
            };
        }
    }
}
