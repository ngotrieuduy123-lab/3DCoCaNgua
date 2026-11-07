using System.Net.Sockets;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("==========================================================");
Console.WriteLine("  GNS301 Console Client for Purchase & Broadcasting Test");
Console.WriteLine("==========================================================");

const string Host = "localhost";
const int Port = 7777;

TcpClient client = new();
try
{
    Console.WriteLine($"[Client] Connecting to TCP server at {Host}:{Port}...");
    await client.ConnectAsync(Host, Port);
    Console.WriteLine("[Client] Connected successfully!");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[Client] Failed to connect: {ex.Message}");
    Console.WriteLine("Make sure the Backend Server is running in VS Code first!");
    Console.ResetColor();
    return;
}

NetworkStream stream = client.GetStream();

// Background task to listen for broadcast messages from server
_ = Task.Run(async () =>
{
    byte[] buffer = new byte[4096];
    StringBuilder sb = new();

    try
    {
        while (client.Connected)
        {
            int read = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (read == 0) break;

            sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
            string current = sb.ToString();
            int nl;
            while ((nl = current.IndexOf('\n')) >= 0)
            {
                string line = current.Substring(0, nl).Trim();
                current = current.Substring(nl + 1);
                sb.Clear();
                sb.Append(current);

                if (!string.IsNullOrEmpty(line))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n----------------------------------------------------------");
                    Console.WriteLine($"[BROADCAST RECEIVED]:\n{line}");
                    Console.WriteLine("----------------------------------------------------------");
                    Console.ResetColor();
                    Console.Write("\nEnter action ('p' for purchase, 'q' to quit): ");
                }
            }
        }
    }
    catch
    {
        // Connection closed
    }
    Console.WriteLine("\n[Client] Disconnected from TCP server.");
});

// Interactive loop for sending purchase messages
while (true)
{
    Console.Write("\nEnter action ('p' for purchase, 'q' to quit): ");
    string? actionChoice = Console.ReadLine()?.Trim().ToLower();

    if (actionChoice == "q" || actionChoice == "exit")
    {
        break;
    }

    if (actionChoice == "p" || string.IsNullOrEmpty(actionChoice))
    {
        Console.Write("Enter playerId (default: player1): ");
        string? playerId = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(playerId)) playerId = "player1";

        Console.Write("Enter SkinKey (default: gundam_unicorn, odin_transformer, arya): ");
        string? skinKey = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(skinKey)) skinKey = "gundam_unicorn";

        Console.Write("Enter skinName (default: Gundam Unicorn): ");
        string? skinName = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(skinName)) skinName = "Gundam Unicorn";

        Console.Write("Enter price (default: 5000): ");
        string? priceStr = Console.ReadLine()?.Trim();
        double price = double.TryParse(priceStr, out double p) ? p : 5000;

        var purchaseObj = new
        {
            action = "PURCHASE",
            skinKey,
            skinName,
            itemId = skinKey,
            fishName = skinName,
            price,
            playerId
        };

        string jsonPayload = JsonSerializer.Serialize(purchaseObj) + "\n";
        byte[] bytes = Encoding.UTF8.GetBytes(jsonPayload);
        await stream.WriteAsync(bytes, 0, bytes.Length);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Client] Purchase message sent for skin '{skinName}'!");
        Console.ResetColor();
    }
}

client.Close();
Console.WriteLine("[Client] Closed.");
