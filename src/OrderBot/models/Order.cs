using System;
using System.Collections.Generic;
using System.Text;

namespace OrderBot.models
{
    public class Order
    {
        public List<Product> Products { get; set; } = new List<Product>();
        public double Total { get; set; } = 0.0;
        public bool ReadyToProcess { get; set; } = false;
        public bool OrderProcessed { get; set; } = false;
        public DateTime OrderDateTime { get; set; }

        public string ItemToAdd { get; set; }
    }
}
