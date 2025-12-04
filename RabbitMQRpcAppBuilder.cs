using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RFRabbitMQRpcApp
{
    public class RabbitMQRpcAppBuilder
        : ConfigurationBuilder
    {
        public IServiceCollection Services { get; } = new ServiceCollection();

        public RabbitMQRpcAppBuilder()
        {
            var Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            this.SetBasePath(Directory.GetCurrentDirectory());
            this.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            this.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
            this.AddJsonFile($"appsettings.{Environment}.json", optional: true, reloadOnChange: true);
        }

        public IConfigurationRoot Configuration()
            => base.Build();

        public new RabbitMQRpcApp Build()
            => RabbitMQRpcApp.Create(this);
    }
}
