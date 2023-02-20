using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Chats
{
    public class Functions
    {
        private readonly ILogger _logger;

        public Functions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Functions>();
        }

        [Function("Index")]
        public HttpResponseData Index([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req, FunctionContext context, ILogger logger)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var path = Path.Combine(context.FunctionDefinition.PathToAssembly, "../index.html");
            var response = req.CreateResponse();
            response.WriteString(File.ReadAllText(path));
            response.Headers.Add("Content-Type", "text/html");
            return response;
        }

        [Function("Negotiate")]
        public static HttpResponseData Negotiate([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequestData req,
        [WebPubSubConnectionInput(UserId = "{query.userId}")] WebPubSubConnection connectionInfo)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteAsJsonAsync(connectionInfo);
            return response;
        }

        [Function("connect")]
        public static WebPubSubEventResponse Connect(
            [WebPubSubTrigger(WebPubSubEventType.System, "connect")] ConnectEventRequest request)
        {
            //Console.WriteLine($"Received client connect with connectionId: {request.ConnectionContext.ConnectionId}");
            //if (request.ConnectionContext.UserId == "attacker")
            //{
            //    return request.CreateErrorResponse(WebPubSubErrorCode.Unauthorized, null);
            //}
            return new ConnectEventResponse
            {
                UserId = request.ConnectionContext.UserId
            };
            //return request.CreateResponse("aaa", null, null, null);
        }

        [Function("connected")]
        public static ConnectedActions Connected(
            [WebPubSubTrigger(WebPubSubEventType.System, "connected")] ConnectedEventRequest request)
        {
            var actions = new ConnectedActions();
            actions.BroadcastAll = new SendToAllAction
            {
                Data = BinaryData.FromString($"[SYSTEM]{request.ConnectionContext.UserId} connected."),
                DataType = WebPubSubDataType.Text
            };

            actions.AddGroup = new AddUserToGroupAction
            {
                UserId = request.ConnectionContext.UserId,
                Group = "group1"
            };
            actions.Callback = new SendToUserAction
            {
                UserId = request.ConnectionContext.UserId,
                Data = BinaryData.FromString($"[SYSTEM]{request.ConnectionContext.UserId} joined group: group1."),
                DataType = WebPubSubDataType.Text
            };

            return actions;
        }

        [Function("broadcast")]
        public static MessageResponse Broadcast(
            [WebPubSubTrigger(WebPubSubEventType.User, "message")] UserEventRequest request, HttpRequestData httpReq)
        {
            var response = new MessageResponse();
            response.BroadcastAll = new SendToAllAction
            {
                Data = BinaryData.FromString($"[From][{request.ConnectionContext.UserId}]: {request.Data}"),
                DataType = WebPubSubDataType.Text
            };

            var wpsResponse = new UserEventResponse
            {
                Data = BinaryData.FromString("[SYSTEM ACK]Messsage delivered."),
                DataType = WebPubSubDataType.Text
            };
            response.Response = wpsResponse;

            return response;
        }

        [Function("disconnected")]
        [WebPubSubOutput]
        public static SendToAllAction Disconnected(
            [WebPubSubTrigger(WebPubSubEventType.System, "disconnected")] DisconnectedEventRequest request)
        {
            Console.WriteLine("Disconnect.");
            return new SendToAllAction
            {
                Data = BinaryData.FromString($"[SYSTEM]{request.ConnectionContext.UserId} disconnect."),
                DataType = WebPubSubDataType.Text
            };
        }
    }

    public class ConnectedActions
    {
        [WebPubSubOutput]
        public SendToAllAction? BroadcastAll { get; set; }

        [WebPubSubOutput]
        public AddUserToGroupAction? AddGroup { get; set; }

        [WebPubSubOutput]
        public SendToUserAction? Callback { get; set; }
    }

    public class MessageResponse
    {
        [WebPubSubOutput]
        public SendToAllAction? BroadcastAll { get; set; }

        public UserEventResponse? Response { get; set; }
    }
}
