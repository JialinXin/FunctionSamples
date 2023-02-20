using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace SimpleOutput;

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

    //[Function("Negotiate")]
    //public static HttpResponseData Negotiate([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequestData req,
    //[SignalRConnectionInfoInput(HubName = "notification")] string connectionInfo)
    //{
    //    var response = req.CreateResponse(HttpStatusCode.OK);
    //    response.Headers.Add("Content-Type", "application/json");
    //    response.WriteString(connectionInfo);
    //    return response;
    //}

    //[Function("Notification")]
    //[SignalROutput(HubName = "notification")]
    //public SignalRMessageAction Notification([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
    //{
    //    _logger.LogInformation("C# HTTP trigger function processed a request.");
    //
    //    return new SignalRMessageAction("newMessage", new object[] { $"[{DateTime.UtcNow}]{Guid.NewGuid()}" });
    //}

    [Function("Negotiate")]
    public static HttpResponseData Negotiate([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequestData req,
    [WebPubSubConnectionInput(Hub = "notification")] WebPubSubConnection connectionInfo)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteAsJsonAsync(connectionInfo);
        return response;
    }

    [Function("Notification")]
    public MultipleOutput Notification([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        var actions = new MultipleOutput();

        //actions.Action1 = new SendToAllAction
        //{
        //    Data = BinaryData.FromString($"[{DateTime.UtcNow}][GUID1]{Guid.NewGuid()}"),
        //    DataType = WebPubSubDataType.Text
        //};
        
        actions.Action2 = new SendToAllAction
        {
            Data = BinaryData.FromString($"[{DateTime.UtcNow}][GUID2]{Guid.NewGuid()}"),
            DataType = WebPubSubDataType.Text
        };
        
        return actions;
    }

}

public class MultipleOutput
{
    [WebPubSubOutput(Hub = "notification")]
    public WebPubSubAction? Action1 { get; set; }
    [WebPubSubOutput(Hub = "notification")]
    public WebPubSubAction? Action2 { get; set; }
}
