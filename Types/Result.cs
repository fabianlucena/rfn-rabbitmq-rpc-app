namespace RFRpcRabbitMQApp.Types
{
    public class Result
    {
        public bool Ok { get; set; } = false;
        public string? Error { get; set; }
        public string? Message { get; set; }
        public int? StatusCode { get; set; }
        public object? Value { get; set; }
    }
}
