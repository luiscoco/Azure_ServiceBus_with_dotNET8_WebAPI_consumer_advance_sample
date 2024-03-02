# How to create .NET8 WebAPI for receiving messages from Azure ServiceBus (Advance sample)

See the source code for this sample in this github repo: https://github.com/luiscoco/Azure_ServiceBus_with_dotNET8_WebAPI_consumer_advance_sample

The provided applications demonstrate some **advanced** features and capabilities of Azure Service Bus

Here's an overview of these features:

**Sessions**: Azure Service Bus supports session-based messaging, which enables you to group related messages and process them in a specific order

In the provided applications, you can send messages with an optional **session ID**

When a **session ID** is specified, the message is associated with that session, and the receiver can process messages from the same session in order

**Scheduled messages**: Azure Service Bus allows you to schedule messages for **future delivery**

In the provided applications, you can send messages with an optional scheduled enqueue time in UTC format

When a scheduled enqueue time is specified, the message will not be available for processing until the specified time

This feature can be useful for implementing delayed processing or time-based workflows

**Message processing options**: The receiver application uses the ServiceBusProcessor class to process messages from the queue

The **ServiceBusProcessorOptions** class provides several configuration options to control how messages are processed, such as:

-**AutoCompleteMessages**: Automatically completes messages after they are processed, indicating that they should be removed from the queue

-**MaxConcurrentCalls**: Specifies the maximum number of concurrent message processing tasks. This can be useful for controlling the processing throughput and resource usage

-**PrefetchCount**: Specifies the number of messages that should be prefetched and cached locally for faster processing

-**ReceiveMode**: Specifies the receive mode for messages, either PeekLock (the default) or ReceiveAndDelete

The **PeekLock mode** allows for at-least-once message processing, while the **ReceiveAndDelete mode** provides simpler, but less reliable, message processing

**Error handling**: The receiver application includes an error handling mechanism to handle any exceptions or errors that occur during message processing

The **ErrorHandler** method is called when an error occurs, allowing you to log the error details, implement retry policies, or perform other error handling actions

**Swagger integration**: Both applications include Swagger (OpenAPI) integration for easy API documentation and testing

Swagger provides an interactive interface to explore and test the API endpoints, making it easier to understand and work with the applications

These advanced features and capabilities enable you to build more robust and flexible messaging solutions using Azure Service Bus

You can further customize and extend these applications to meet your specific requirements, such as implementing **custom message processing logic**,

adding **authentication and authorization**, or **integrating with other Azure services**

## 1. Create Azure ServiceBus (queue)

We first log in to Azure Portal and search for Azure Service Bus 

![image](https://github.com/luiscoco/Azure_ServiceBus_with_dotNET8_WebAPI_consumer/assets/32194879/c1083a36-37ed-41cd-b338-05b79338d256)

We create a new Azure Service Bus 

![image](https://github.com/luiscoco/Azure_ServiceBus_with_dotNET8_WebAPI_consumer/assets/32194879/c55dfe80-c170-4a11-abd5-64fba5d3d038)

We input the required data: Subscription, ResourceGroup, Namespace, location and pricing tier

![image](https://github.com/luiscoco/Azure_ServiceBus_with_dotNET8_WebAPI_consumer/assets/32194879/ceb2546c-a073-41c7-8ec5-0f29e59766fb)

We verify the new Azure Service Bus

![image](https://github.com/luiscoco/Azure_ServiceBus_with_dotNET8_WebAPI_consumer/assets/32194879/d5d306e4-cea0-4898-a9e5-9b0ebb6d9eca)

We get the **connection string**

![image](https://github.com/luiscoco/Azure_ServiceBus_with_dotNET8_WebAPI_consumer/assets/32194879/d540d906-ce3b-4d5d-b984-563a1895654b)

![image](https://github.com/luiscoco/Azure_ServiceBus_with_dotNET8_WebAPI_consumer/assets/32194879/8c077842-6b05-46e2-a03e-de04f8bd1dcf)

This is the connection string:

```
Endpoint=sb://myservicebus1974.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=c/7ve5kw9QuPqM8YSUWQvNTrjM+y5hkmp+ASbE85qY4=
```

We have to create a new **topic**

![image](https://github.com/luiscoco/Azure_ServiceBus_with_dotNET8_WebAPI_consumer/assets/32194879/4042c8cc-f5f3-4e0e-9dfc-139722d6297d)

We also have to create a **queue**

![image](https://github.com/luiscoco/Azure_ServiceBus_with_dotNET8_WebAPI_consumer_advance_sample/assets/32194879/618ca442-e78d-4e65-a6d4-50255dd0af1b)
 
## 2. Create a .NET8 WebAPI with VSCode



## 3. Load project dependencies

```
dotnet add package Azure.Messaging.ServiceBus
```


![image](https://github.com/luiscoco/Azure_ServiceBus_with_dotNET8_WebAPI_consumer_advance_sample/assets/32194879/ae6a45e0-42d4-4007-ae38-09eba05afc2e)


## 4. Create the project structure

![image](https://github.com/luiscoco/Azure_ServiceBus_with_dotNET8_WebAPI_consumer_advance_sample/assets/32194879/65e7c7a5-296f-42e6-a31c-69557a71674c)

## 5. Create the Controller

```csharp
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Mvc;

namespace ServiceBusReceiverApi.Controllers
{
    public class MessageDto
    {
        public string? Body { get; set; }
        public string? SessionId { get; set; }
        public DateTime? ScheduledEnqueueTimeUtc { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class ServiceBusReceiverController : ControllerBase
    {
        private static string connectionString = "Endpoint=sb://myservicebus1974.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=c/7ve5kw9QuPqM8YSUWQvNTrjM+y5hkmp+ASbE85qY4=";
        private static string queueName = "myqueue";
        private static ServiceBusClient client;
        private static ServiceBusProcessor processor;
        private static ConcurrentQueue<MessageDto> receivedMessages = new ConcurrentQueue<MessageDto>();

        static ServiceBusReceiverController()
        {
            client = new ServiceBusClient(connectionString);

            var processorOptions = new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = true,
                MaxConcurrentCalls = 10,
                PrefetchCount = 100,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            };

            processor = client.CreateProcessor(queueName, processorOptions);
            processor.ProcessMessageAsync += MessageHandler;
            processor.ProcessErrorAsync += ErrorHandler;
        }

        public static async Task StartMessageProcessing()
        {
            await processor.StartProcessingAsync();
        }

        [HttpGet("receive")]
        public ActionResult<IEnumerable<MessageDto>> ReceiveMessages()
        {
            return receivedMessages.ToList();
        }

        static async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            string sessionId = args.Message.SessionId;
            DateTime? scheduledEnqueueTimeUtc = args.Message.ScheduledEnqueueTime.DateTime;

            Console.WriteLine($"Received message: {body}, SessionId: {sessionId}, ScheduledEnqueueTimeUtc: {scheduledEnqueueTimeUtc}");

            receivedMessages.Enqueue(new MessageDto { Body = body, SessionId = sessionId, ScheduledEnqueueTimeUtc = scheduledEnqueueTimeUtc });

            await args.CompleteMessageAsync(args.Message);
        }

        static async Task ErrorHandler(ProcessErrorEventArgs args)
        {
            Console.WriteLine($"Error source: {args.ErrorSource}");
            Console.WriteLine($"Fully qualified namespace: {args.FullyQualifiedNamespace}");
            Console.WriteLine($"Entity path: {args.EntityPath}");
            Console.WriteLine($"Exception: {args.Exception.Message}");

            if (args.Exception is ServiceBusException serviceBusException)
            {
                // Handle ServiceBusException
            }

            await Task.CompletedTask;
        }
    }
}
```

## 6. Modify the application middleware(program.cs)

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.OpenApi.Models;
using ServiceBusReceiverApi.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ServiceBusReceiverApi", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRouting();

// Enable middleware to serve generated Swagger as a JSON endpoint.
app.UseSwagger();

// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ServiceBusReceiverApi v1");
});

app.UseAuthorization();

app.MapControllers();

// Start message processing for the receiver project


ServiceBusReceiverController.StartMessageProcessing().Wait();

app.Run();
```

## 7. Run and Test the application



