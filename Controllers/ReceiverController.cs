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
