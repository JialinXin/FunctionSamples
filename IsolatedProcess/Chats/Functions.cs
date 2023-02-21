using System.Net;
using System.Text.Json.Nodes;
using Azure;
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


        #region WebPubSubTrigger

        [Function("trigger-connect")]
        public static WebPubSubEventResponse TriggerConnect(
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

        [Function("trigger-connected")]
        public static ConnectedActions TriggerConnected(
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

        [Function("trigger-broadcast")]
        public static MessageResponse TriggerBroadcast(
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

        [Function("trigger-disconnected")]
        [WebPubSubOutput]
        public static SendToAllAction TriggerDisconnected(
            [WebPubSubTrigger(WebPubSubEventType.System, "disconnected")] DisconnectedEventRequest request)
        {
            Console.WriteLine("Disconnect.");
            return new SendToAllAction
            {
                Data = BinaryData.FromString($"[SYSTEM]{request.ConnectionContext.UserId} disconnect."),
                DataType = WebPubSubDataType.Text
            };
        }

        #endregion

        #region WebPubSubContext
        // validate method when upstream set as http://<func-host>/api/{event}
        [Function("validate")]
        public static HttpResponseData Validate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "options")] HttpRequestData req,
            [WebPubSubContextInput] WebPubSubContext wpsReq)
        {
            return BuildHttpResponseData(req, wpsReq.Response);
        }

        [Function("connect")]
        public static HttpResponseData Connect([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequestData req,
            [WebPubSubContextInput] WebPubSubContext wpsReq)
        {
            var response = req.CreateResponse();
            Console.WriteLine($"Received client connect request.");
            if (wpsReq.Request is PreflightRequest || wpsReq.ErrorMessage != null)
            {
                response.WriteAsJsonAsync(wpsReq.Response);
                return response;
            }
            var request = wpsReq.Request as ConnectEventRequest;
            // assign the properties if needed.
            response.WriteAsJsonAsync(request.CreateResponse(request.ConnectionContext.UserId, null, null, null));
            return response;
        }

        [Function("connected")]
        public static ConnectedActions Connected([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequestData req,
            [WebPubSubContextInput] WebPubSubContext wpsReq)
        {
            var actions = new ConnectedActions();
            actions.BroadcastAll = new SendToAllAction
            {
                Data = BinaryData.FromString($"[SYSTEM]{wpsReq.Request.ConnectionContext.UserId} connected."),
                DataType = WebPubSubDataType.Text
            };

            actions.AddGroup = new AddUserToGroupAction
            {
                UserId = wpsReq.Request.ConnectionContext.UserId,
                Group = "group1"
            };
            actions.Callback = new SendToUserAction
            {
                UserId = wpsReq.Request.ConnectionContext.UserId,
                Data = BinaryData.FromString($"[SYSTEM]{wpsReq.Request.ConnectionContext.UserId} joined group: group1."),
                DataType = WebPubSubDataType.Text
            };

            return actions;
        }

        [Function("message")]
        public static MessageResponse1 Broadcast([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequestData req,
            [WebPubSubContextInput] WebPubSubContext wpsReq)
        {
            var response = new MessageResponse1();
            response.Response = req.CreateResponse();

            if (wpsReq.Request is PreflightRequest || wpsReq.ErrorMessage != null)
            {
                response.Response.WriteAsJsonAsync(wpsReq.Response);
                return response;
            }
            if (wpsReq.Request is UserEventRequest request)
            {
                response.BroadcastAll = new SendToAllAction
                {
                    Data = request.Data,
                    DataType = request.DataType
                };
            }

            response.Response.WriteAsJsonAsync(new UserEventResponse("[SYSTEM]ACK"));
            return response;
        }

        [Function("disconnected")]
        [WebPubSubOutput]
        public static SendToAllAction Disconnected([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequestData req,
            [WebPubSubContextInput] WebPubSubContext wpsReq)
        {
            Console.WriteLine("Disconnect.");
            return new SendToAllAction
            {
                Data = BinaryData.FromString($"[SYSTEM]{wpsReq.Request.ConnectionContext.UserId} disconnect."),
                DataType = WebPubSubDataType.Text
            };
        }
        #endregion

        public static HttpResponseData BuildHttpResponseData(HttpRequestData request, SimpleResponse wpsResponse)
        {
            var response = request.CreateResponse();
            response.StatusCode = (HttpStatusCode)wpsResponse.Status;
            response.Body = response.Body;
            foreach (var header in wpsResponse.Headers)
            {
                response.Headers.Add(header.Key, header.Value);
            }
            return response;
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

    public class MessageResponse1
    {
        [WebPubSubOutput]
        public SendToAllAction? BroadcastAll { get; set; }

        public HttpResponseData? Response { get; set; }
    }
}
