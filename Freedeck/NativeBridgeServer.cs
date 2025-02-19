using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSCore;
using CSCore.Codecs;
using CSCore.SoundOut;

namespace Freedeck;

public class NBSDataStructure
{
    public string Event { get; set; }
    public string[] Data { get; set; }
}

public class NativeBridgeServer
{
    private HttpListener _httpListener;
    public static Dictionary<string, Action<WebSocket, string[]>> _callback = new Dictionary<string, Action<WebSocket, string[]>>();
    private string uriPrefix;
    
    public NativeBridgeServer(string uriPrefix)
    {
        _httpListener = new HttpListener();
        this.uriPrefix = uriPrefix;
        _httpListener.Prefixes.Add(uriPrefix);
    }
    
    public void ListenForEvent(string ev, Action<WebSocket, string[]> callback)
    {
        _callback.Add(ev, callback);
    }

    public async Task StartAsync()
    {
        MainWindow.Log("NativeBridgeServer", "NBWS starting..."); 
        _httpListener.Start();
        MainWindow.Log("NativeBridgeServer", "NBWS opened on " + uriPrefix); 
        
        while (true)
        {
            var httpContext = await _httpListener.GetContextAsync();
            if (httpContext.Request.IsWebSocketRequest)
            {
                var webSocketContext = await httpContext.AcceptWebSocketAsync(null);
                _ = HandleWebSocketAsync(webSocketContext.WebSocket);
            }
            else
            {
                httpContext.Response.StatusCode = 400;
                httpContext.Response.Close();
            }
        }
    }

    public void SendFDPacket(NBSDataStructure data, WebSocket socket)
    {
        var datOne = JsonSerializer.Serialize(data, new JsonSerializerOptions()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        var dat = Convert.ToBase64String(Encoding.UTF8.GetBytes(datOne));
                
        socket.SendAsync(new ArraySegment<byte>(
            Encoding.UTF8.GetBytes(dat)
        ), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task HandleWebSocketAsync(WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
    
        while (result.MessageType != WebSocketMessageType.Close)
        {
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            try
            {
                NBSDataStructure? data = JsonSerializer.Deserialize<NBSDataStructure>(message);
                if (_callback.ContainsKey(data.Event))
                {
                    await Task.Run(() =>
                    { 
                        _callback[data.Event](webSocket, data.Data);
                    });
                }
            }
            catch (Exception e)
            {
                SendFDPacket(new NBSDataStructure()
                {
                    Event = "error",
                    Data = new string[] { e.Message }
                }, webSocket);
            }

            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        webSocket.Dispose();
    }
}