# RFRabbitMQRPCApp

> 🇪🇸 Español | 🇺🇸 [English Version](README.md)

**RFRabbitMQRPCApp** es una librería .NET para publicar y gestionar **servicios RPC** utilizando **RabbitMQ** como middleware de mensajería.
Proporciona una abstracción que simplifica la creación de microservicios RPC fuertemente tipados, manejando automáticamente el enlace de colas,
inyección de dependencias, logging y enrutamiento de mensajes.

---

## 🚀 Características

- Publicar servicios RPC usando controladores con atributos.
- Manejo automático de conexión y canales de RabbitMQ.
- Enrutamiento de mensajes usando `[Queue("name")]`.
- Soporte para DI e `ILogger`.
- Serialización integrada para request/response.
- Funciona junto con:
  - `RFRabbitMQ`
  - `RFRabbitMQRPCClient`

---

## 📦 Instalación

### NuGet
```bash
Install-Package RFRabbitMQRPCApp
```

### .NET CLI
```bash
dotnet add package RFRabbitMQRPCApp
```

---

## 🔧 Configuración

Ejemplo de `appsettings.json`:

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

# 🖥️ Ejemplo — Crear un microservicio RPC

---

## 1️⃣ Definir un servicio

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
            Property = "Valor de propiedad",
            OtherProperty = "Otro valor de propiedad"
        };
    }
}
```

---

## 2️⃣ Crear un controlador RPC

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

## 3️⃣ Hospedar la aplicación RPC

```csharp
var builder = RabbitMQRpcApp.CreateBuilder();

builder.Services.AddScoped<IDemoService, DemoService>();

var app = builder.Build();

app.Run(app => app.Logger.LogInformation("Demo microservice initiated"));
```

---

# 🧩 Casos de uso

- Microservicios que requieren respuestas RPC síncronas.
- Validaciones en tiempo real.
- Motores de precios/cálculos.
- Servicios RPC de autenticación/autorización.
- Mensajería request/response sin usar REST.

---

# 🔍 Versionado

Versión actual: **1.3.1**

---

# 📚 Dependencias

Este paquete depende de:

- RabbitMQ.Client 7.2.0  
- RFRabbitMQRPCClient 1.3.1  
- Microsoft.Extensions.Configuration.* 8.0.0  
- Microsoft.Extensions.DependencyInjection 8.0.0  
- Microsoft.Extensions.Logging 8.0.0  

---

# 📄 Licencia

Licencia MIT.

---

# 🌐 Repositorio

https://github.com/fabianlucena/rfn-rabbitmq-rpc-app
