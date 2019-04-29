using OrderBot.models;

namespace OrderBot.services
{
    public interface IOrdersService
    {
        void CreateOrder(Order order);
    }
}
