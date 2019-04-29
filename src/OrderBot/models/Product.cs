using System;
using System.Collections.Generic;
using System.Text;

namespace OrderBot.models
{
    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }

        public override string ToString()
        {
            return $"{Name} : ${Price:0.00}";
        }

        public string ExtendedDescription()
        {
            return $"{Name}. {Description} Cost: {Price:0.00}";
        }
    }
}
