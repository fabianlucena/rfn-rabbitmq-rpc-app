namespace RFRabbitMQRpcApp.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class Queue(string name)
        : Attribute
    {
        public string Name { get; } = name;
    }
}
