# RFRabbitMQRPCApp

> 🇺🇸 English | 🇪🇸 [Versión en Español](https://github.com/fabianlucena/rfn-rabbitmq-rpc-app/blob/main/README.es.md)
> [Video tutorial](https://www.youtube.com/watch?v=hrU-upEMlPk)

**RFRabbitMQRPCApp** is a .NET library for hosting and managing **RPC services** using **RabbitMQ** as the messaging middleware.
It provides an abstraction that simplifies building strongly-typed RPC microservices, handling queue binding, dependency injection, logging,
and message routing automatically.

---

## 🚀 Features

- Host RPC services with simple attribute-based controllers.
- Automatic RabbitMQ connection + channel management.
- Queue routing using `[Queue("name")]`.
- Support for dependency injection and logging (`ILogger`).
- Built-in request/response serialization.
- Works seamlessly with:
  - `RFRabbitMQ`
  - `RFRabbitMQRPCClient`

---

## 📦 Installation

### NuGet
```bash
Install-Package RFRabbitMQRPCApp
```

### .NET CLI
```bash
dotnet add package RFRabbitMQRPCApp
```

---

## 🔧 Configuration

Example `appsettings.json`:

```json
{
  "RabbitMQ": {
    "HostName": "localhost",
    "UserName": "guest",
    "Password": "guest",
    "ServiceQueuePrefix": "rpc-app-"
  }
}
```

---

# 🖥️ Example — Creating an RPC Microservice

Below is a complete example based on real files.

---

## 1️⃣ Define a service

```csharp
internal interface IDemoService
{
    object GetDemoData();
}
```

```csharp
public class DemoService : IDemoService
{
    public object GetDemoData()
    {
        return new
        {
            Property = "Property value",
            OtherProperty = "Other property value "
        };
    }
}
```

---

## 2️⃣ Create an RPC Controller

```csharp
[RpcController]
internal class DemoController(
    ILogger<DemoController> logger,
    IDemoService demoService
) : Controller
{
    [Queue("my-first-queue")]
    public async Task<Result> MyFirstQueue()
    {
        logger.LogInformation("Received request my-first-queue");

        return Ok(demoService.GetDemoData());
    }
}
```

---

## 3️⃣ Host the RPC Application

```csharp
var builder = RabbitMQRpcApp.CreateBuilder();

builder.Services.AddScoped<IDemoService, DemoService>();

var app = builder.Build();

app.Run(app => app.Logger.LogInformation("Demo microservice initiated"));
```

---

# 🧩 Use Cases

- Microservices requiring synchronous RPC responses.
- Real-time validations.
- Pricing/Calculation engines.
- Authentication/authorization RPC providers.
- Request/response messaging without REST.

---

# 🔍 Versioning

Current version: **1.3.3**

---

# 📚 Dependencies

This package depends on:

- RabbitMQ.Client 7.2.0  
- RFRabbitMQRPCClient 1.3.3  
- Microsoft.Extensions.Configuration.* 8.0.0  
- Microsoft.Extensions.DependencyInjection 8.0.0  
- Microsoft.Extensions.Logging 8.0.0  

---

# 📄 License

MIT License.

---

# 🌐 Repository

https://github.com/fabianlucena/rfn-rabbitmq-rpc-app
