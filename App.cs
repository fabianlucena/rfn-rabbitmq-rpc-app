using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RFRabbitMQ;
using RFRabbitMQRpcClient.Types;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RFRabbitMQRpcApp.Attributes;

namespace RFRabbitMQRpcApp
{
    public class App
    {
        public RabbitMQOptions Options { get; }
        private IServiceCollection Services { get; }
        private ConnectionFactory ConnectionFactory { get; }
        public ILogger Logger { get; set; }

        private IConnection? _connection = null;
        private IChannel? _channel = null;

        public App(RabbitMQOptions options, IServiceCollection? services)
        {
            Options = options;
            Services = services ?? new ServiceCollection();
            ConnectionFactory = new ConnectionFactory
            {
                HostName = Options.HostName,
                Port = Options.Port,
                Ssl = Options.Ssl,
                UserName = Options.UserName,
                Password = Options.Password,
            };

            if (!Services.Any(sd => sd.ServiceType == typeof(ILoggerFactory)))
            {
                Services.AddLogging(config =>
                {
                    config.ClearProviders();
                    config.AddConsole();
                    config.SetMinimumLevel(LogLevel.Information);
                });
            }

            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            Logger = loggerFactory.CreateLogger<App>();
        }

        public static IConfigurationBuilder CreateBuilder()
        {
            var Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment}.json", optional: true, reloadOnChange: true);

            return builder;
        }

        public static App Create(RabbitMQOptions options, IServiceCollection? services = null)
            => new(options, services);

        public static App Create(IConfiguration? configuration = null, IServiceCollection? services = null)
        {
            configuration ??= CreateBuilder().Build();

            var options = new RabbitMQOptions(configuration.GetSection("RabbitMQ"));
            options.Configure(configuration.GetSection("RpcRabbitMQ"));
            return new(options, services);
        }

        public static IEnumerable<Type> GetControllers()
        {
            var rpcControllerType = typeof(RpcController);
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.GetCustomAttribute(rpcControllerType) != null);
        }

        public static IEnumerable<MethodInfo> GetMethods(Type controller)
        {
            var queueType = typeof(Queue);
            return controller.GetMethods()
                .Where(m => m.GetCustomAttribute(queueType) != null);
        }

        public async Task MapControllersAsync()
        {
            _connection ??= await ConnectionFactory.CreateConnectionAsync();
            _channel ??= await _connection.CreateChannelAsync();

            var controllers = GetControllers();
            foreach (var controller in controllers)
                Services.AddScoped(controller);

            var serviceProvider = Services.BuildServiceProvider();
            foreach (var controller in controllers)
            {
                var methods = GetMethods(controller);
                foreach (var method in methods)
                {
                    var queueAttribute = method.GetCustomAttribute<Queue>();
                    if (queueAttribute == null)
                        continue;

                    var queue = queueAttribute.Name?.Trim();
                    if (string.IsNullOrEmpty(queue))
                        continue;

                    var instance = serviceProvider.GetRequiredService(controller);
                    var methodInfo = controller.GetMethod(method.Name, BindingFlags.Public | BindingFlags.Instance);
                    if (methodInfo == null)
                        continue;

                    await _channel.QueueDeclareAsync(queue: queue, durable: false, exclusive: false, autoDelete: false, arguments: null);
                    await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

                    var consumer = new AsyncEventingBasicConsumer(_channel);
                    consumer.ReceivedAsync += async (sender, ea) =>
                    {
                        Response? response = null;
                        try
                        {
                            using var scope = serviceProvider.CreateScope();

                            var instance = scope.ServiceProvider.GetRequiredService(controller) as Controller
                                ?? throw new InvalidOperationException($"Controller {controller.Name} is not derivated from Controller class.");

                            var methodInfo = controller.GetMethod(method.Name, BindingFlags.Public | BindingFlags.Instance)
                                ?? throw new InvalidOperationException($"Method {method.Name} not found in controller {controller.Name}.");

                            instance.Sender = sender;
                            instance.AsyncEventArgs = ea;
                            instance.Body = ea.Body.ToArray();

                            List<object?> parameters = [];
                            var jsonBody = Encoding.UTF8.GetString(instance.Body ?? []);
                            if (!string.IsNullOrEmpty(jsonBody))
                            {
                                var parameterInfos = methodInfo.GetParameters();
                                foreach (var paramInfo in parameterInfos)
                                {
                                    Type paramType = paramInfo.ParameterType;
                                    if (paramType == null)
                                        continue;

                                    object? deserialized = null;
                                    if (paramType.IsPrimitive ||
                                        paramType == typeof(string) ||
                                        paramType == typeof(decimal) ||
                                        paramType == typeof(DateTime) ||
                                        paramType == typeof(Guid) ||
                                        paramType.IsEnum
                                    )
                                    {
                                        try
                                        {
                                            using var doc = JsonDocument.Parse(jsonBody);

                                            JsonElement root = doc.RootElement;

                                            if (root.ValueKind == JsonValueKind.Object && paramInfo.Name != null &&
                                                root.TryGetProperty(paramInfo.Name, out JsonElement elem))
                                            {
                                                if (paramType.IsEnum)
                                                    deserialized = Enum.Parse(paramType, elem.GetString() ?? "", ignoreCase: true);
                                                else 
                                                    deserialized = Convert.ChangeType(elem.ToString(), paramType);
                                            }
                                            else if (root.ValueKind == JsonValueKind.String || root.ValueKind == JsonValueKind.Number || root.ValueKind == JsonValueKind.True || root.ValueKind == JsonValueKind.False)
                                            {
                                                deserialized = Convert.ChangeType(root.ToString(), paramType);
                                            }

                                        }
                                        catch
                                        {
                                            continue; // skip si la conversión falla
                                        }
                                    }
                                    else
                                    {
                                        deserialized = JsonSerializer.Deserialize(jsonBody, paramType);
                                    }

                                    if (deserialized != null)
                                        parameters.Add(deserialized);
                                }
                            }

                            var asyncResult = methodInfo.Invoke(instance, [..parameters]);
                            object? result;
                            if (asyncResult is Task task)
                            {
                                await task.ConfigureAwait(false);
                                result = task.GetType().GetProperty("Result")?.GetValue(task);
                            }
                            else
                                result = asyncResult;

                            if (result != null)
                            {
                                response = result as Response
                                    ?? new Response(result);
                            }
                        }
                        catch (Exception e)
                        {
                            e = e.InnerException ?? e;
                            Logger.LogError(e, "{Error}: {Message}", e.GetType().Name, e.Message);
                            response = new Response(
                                new Result
                                {
                                    Ok = false,
                                    Error = e.GetType().Name,
                                    Message = e.Message,
                                    StatusCode = 500
                                }
                            );
                        }

                        AsyncEventingBasicConsumer cons = (AsyncEventingBasicConsumer)sender;
                        IChannel ch = cons.Channel;
                        if (!ch.IsOpen)
                        {
                            Logger.LogWarning("Channel {Queue} is closed or disposed.", queue);
                            return;
                        }

                        IReadOnlyBasicProperties props = ea.BasicProperties;
                        var replyProps = new BasicProperties
                        {
                            CorrelationId = props.CorrelationId
                        };

                        await ch.BasicPublishAsync(
                            exchange: string.Empty,
                            routingKey: props.ReplyTo!,
                            mandatory: true,
                            basicProperties: replyProps,
                            body: response?.GetBytes()
                        );
                        await ch.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                    };

                    await _channel.BasicConsumeAsync(queue, false, consumer);
                }
            }
        }

        public void Run(Action<App>? onRun = null)
        {
            RunAsync(onRun)
                .GetAwaiter()
                .GetResult();
        }

        public async Task RunAsync(Action<App>? onRun = null)
        {
            await MapControllersAsync();
            onRun?.Invoke(this);

            bool isTest = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.FullName is not null
                    && (a.FullName.StartsWith("xunit")
                        || a.FullName.StartsWith("NUnit")
                        || a.FullName.Contains("Microsoft.VisualStudio.TestPlatform")
                    )
                );

            if (!isTest)
                await Task.Delay(Timeout.Infinite);
        }
    }
}
