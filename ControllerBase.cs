using RabbitMQ.Client.Events;
using RFRabbitMQClient.Types;

namespace RFRabbitMQRpcApp
{
    public class ControllerBase
    {
        public object? Sender { get; set; }
        public AsyncEventArgs AsyncEventArgs { get; set; } = null!;
        public byte[] Body { get; set; } = null!;

        public static Result Ok(object? value = null)
        {
            return new Result()
            {
                Ok = true,
                Value = value
            };
        }
    }
}
