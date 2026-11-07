using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CoCaNgua.Server.Models;
using CoCaNgua.Server.Repositories;

namespace CoCaNgua.Server.Services;

public class StateObject
{
    public const int BufferSize = 4096;
    public TcpClient Client { get; set; } = null!;
    public NetworkStream Stream { get; set; } = null!;
    public byte[] Buffer { get; set; } = new byte[BufferSize];
    public StringBuilder Sb { get; set; } = new();
    public string ClientId { get; set; } = Guid.NewGuid().ToString();
}

public class PurchaseMessage
{
    public string? action { get; set; }
    public string? skinKey { get; set; }
    public string? skinName { get; set; }
    public string? itemId { get; set; } // fallback
    public string? fishName { get; set; } // fallback
    public double price { get; set; }
    public string? playerId { get; set; }
}

public class BroadcastMessage
{
    public string action { get; set; } = "BROADCAST";
    public string? playerId { get; set; }
    public string? skinKey { get; set; }
    public string? skinName { get; set; }
    public string? fishName { get; set; } // fallback
    public double price { get; set; }
    public string? message { get; set; }
}

public class TcpBroadcastServer : BackgroundService
{
    private readonly ILogger<TcpBroadcastServer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _port;
    private TcpListener? _listener;
    private readonly ConcurrentDictionary<string, StateObject> _clients = new();

    public TcpBroadcastServer(
        ILogger<TcpBroadcastServer> logger,
        IConfiguration config,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _port = int.TryParse(config["TcpServer:Port"], out int p) ? p : 7777;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _logger.LogInformation("[TCP Server] Listening for connections on port {Port}...", _port);

            stoppingToken.Register(() =>
            {
                try { _listener?.Stop(); } catch { }
            });

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(stoppingToken);
                    var state = new StateObject
                    {
                        Client = client,
                        Stream = client.GetStream()
                    };

                    _clients[state.ClientId] = state;
                    _logger.LogInformation("[TCP Server] Client connected: {ClientId} from {RemoteEndPoint}",
                        state.ClientId, client.Client.RemoteEndPoint);

                    _ = HandleClientAsync(state, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[TCP Server] Error accepting client connection");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TCP Server] Critical server error on port {Port}", _port);
        }
        finally
        {
            _listener?.Stop();
            _logger.LogInformation("[TCP Server] Stopped.");
        }
    }

    private async Task HandleClientAsync(StateObject state, CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested && state.Client.Connected)
            {
                int bytesRead = await state.Stream.ReadAsync(state.Buffer, 0, StateObject.BufferSize, stoppingToken);
                if (bytesRead == 0)
                {
                    break; // Client disconnected gracefully
                }

                string chunk = Encoding.UTF8.GetString(state.Buffer, 0, bytesRead);
                state.Sb.Append(chunk);

                ProcessBufferLines(state);
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            // Normal disconnect
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[TCP Server] Client {ClientId} communication error: {Message}", state.ClientId, ex.Message);
        }
        finally
        {
            DisconnectClient(state);
        }
    }

    private void ProcessBufferLines(StateObject state)
    {
        string currentContent = state.Sb.ToString();
        int newlineIndex;

        while ((newlineIndex = currentContent.IndexOf('\n')) >= 0)
        {
            string line = currentContent.Substring(0, newlineIndex).Trim();
            currentContent = currentContent.Substring(newlineIndex + 1);
            state.Sb.Clear();
            state.Sb.Append(currentContent);

            if (!string.IsNullOrWhiteSpace(line))
            {
                _ = HandleMessageAsync(state, line);
            }
        }
    }

    private async Task HandleMessageAsync(StateObject senderState, string jsonLine)
    {
        try
        {
            _logger.LogInformation("[TCP Server] Received from {ClientId}: {Line}", senderState.ClientId, jsonLine);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var message = JsonSerializer.Deserialize<PurchaseMessage>(jsonLine, options);

            if (message != null && string.Equals(message.action, "PURCHASE", StringComparison.OrdinalIgnoreCase))
            {
                await ProcessPurchaseAsync(senderState, message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TCP Server] Failed to parse message from client {ClientId}", senderState.ClientId);
        }
    }

    private async Task ProcessPurchaseAsync(StateObject senderState, PurchaseMessage purchase)
    {
        string playerId = purchase.playerId ?? "UnknownPlayer";
        string skinKey = !string.IsNullOrWhiteSpace(purchase.skinKey) ? purchase.skinKey : (!string.IsNullOrWhiteSpace(purchase.itemId) ? purchase.itemId : "default_horse");
        string skinName = !string.IsNullOrWhiteSpace(purchase.skinName) ? purchase.skinName : (!string.IsNullOrWhiteSpace(purchase.fishName) ? purchase.fishName : skinKey);
        double price = purchase.price;

        Console.WriteLine($"[TCP Server] PURCHASE received: Player '{playerId}' bought skin '{skinName}' ({skinKey}) for {price:N0} coins");

        string purchaseLogId = string.Empty;

        // 1. Save into MongoDB purchaselogs collection
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var logRepo = scope.ServiceProvider.GetRequiredService<IPurchaseLogRepository>();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var logEntry = new PurchaseLog
            {
                PlayerId = playerId,
                ItemId = skinKey,
                SkinKey = skinKey,
                SkinName = skinName,
                Price = price,
                Status = "RECEIVED",
                CreatedAt = DateTime.UtcNow
            };

            await logRepo.CreateAsync(logEntry);
            purchaseLogId = logEntry.Id;

            // Also deduct coins / add skin in player profile if found
            var user = await userRepo.GetByUsernameAsync(playerId);
            if (user != null)
            {
                if (!string.IsNullOrEmpty(skinKey) && !user.OwnedSkinIds.Contains(skinKey))
                {
                    user.OwnedSkinIds.Add(skinKey);
                }
                user.Coins = Math.Max(0, user.Coins - (int)price);
                await userRepo.UpdateAsync(user);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TCP Server] Error saving purchase to MongoDB");
        }

        // 2. Broadcast to all other connected clients
        var broadcast = new BroadcastMessage
        {
            action = "BROADCAST",
            playerId = playerId,
            skinKey = skinKey,
            skinName = skinName,
            fishName = skinName,
            price = price,
            message = $"Người chơi {playerId} vừa sở hữu skin {skinName} với giá {price:N0} coins!"
        };

        string broadcastJson = JsonSerializer.Serialize(broadcast) + "\n";
        byte[] broadcastBytes = Encoding.UTF8.GetBytes(broadcastJson);

        int sentCount = 0;
        foreach (var pair in _clients)
        {
            if (pair.Key != senderState.ClientId && pair.Value.Client.Connected)
            {
                try
                {
                    await pair.Value.Stream.WriteAsync(broadcastBytes, 0, broadcastBytes.Length);
                    sentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[TCP Server] Failed to send broadcast to {ClientId}: {Error}", pair.Key, ex.Message);
                }
            }
        }

        _logger.LogInformation("[TCP Server] Broadcasted purchase of '{Item}' to {Count} other clients.", skinName, sentCount);

        // 3. Update status in MongoDB to "BROADCASTED"
        if (!string.IsNullOrEmpty(purchaseLogId))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var logRepo = scope.ServiceProvider.GetRequiredService<IPurchaseLogRepository>();
                var actionLogRepo = scope.ServiceProvider.GetRequiredService<IActionLogRepository>();

                await logRepo.UpdateStatusAsync(purchaseLogId, "BROADCASTED");

                string plaintextLog = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] [TCP_PURCHASE] Player='{playerId}' | Skin='{skinName}' (Key: {skinKey}) | PricePaid={price:N0} Coins | Status=BROADCASTED | ClientsNotified={sentCount} | ClientId='{senderState.ClientId}'";
                await actionLogRepo.LogAsync(plaintextLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TCP Server] Error updating log status to BROADCASTED");
            }
        }
    }

    private void DisconnectClient(StateObject state)
    {
        _clients.TryRemove(state.ClientId, out _);
        try { state.Stream.Close(); } catch { }
        try { state.Client.Close(); } catch { }
        _logger.LogInformation("[TCP Server] Client disconnected: {ClientId}", state.ClientId);
    }
}
