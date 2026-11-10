using CoCaNgua.Server.Models;
using CoCaNgua.Server.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CoCaNgua.Server.Services;

public static class DbInitializer
{
    public static bool IsMongoConnected { get; set; } = false;

    public static async Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        try
        {
            var db = services.GetRequiredService<IMongoDatabase>();
            var userRepo = services.GetRequiredService<IUserRepository>();
            var arenaRepo = services.GetRequiredService<IArenaRepository>();
            var skinRepo = services.GetRequiredService<ISkinRepository>();
            var actionLogRepo = services.GetRequiredService<IActionLogRepository>();

            // Ping database with a 5-second timeout (accommodates cloud/Atlas TLS latency)
            using var pingCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: pingCts.Token);
            IsMongoConnected = true;
            logger.LogInformation("[MongoDB] Successfully connected to database: {DatabaseName}", db.DatabaseNamespace.DatabaseName);

            // Clean up obsolete legacy collections from ASM if present
            try
            {
                var collections = (await db.ListCollectionNamesAsync()).ToList();
                if (collections.Contains("ponds")) await db.DropCollectionAsync("ponds");
                if (collections.Contains("fishes")) await db.DropCollectionAsync("fishes");
            }
            catch { }

            // Create Indexes for Cờ Cá Ngựa domain
            var usersCol = db.GetCollection<User>("users");
            await usersCol.Indexes.CreateOneAsync(
                new CreateIndexModel<User>(
                    Builders<User>.IndexKeys.Ascending(x => x.Username),
                    new CreateIndexOptions { Unique = true }
                )
            );

            var arenasCol = db.GetCollection<Arena>("arenas");
            await arenasCol.Indexes.CreateOneAsync(
                new CreateIndexModel<Arena>(
                    Builders<Arena>.IndexKeys.Ascending(x => x.ArenaName),
                    new CreateIndexOptions { Unique = true }
                )
            );

            var skinsCol = db.GetCollection<HorseSkin>("skins");
            await skinsCol.Indexes.CreateOneAsync(
                new CreateIndexModel<HorseSkin>(
                    Builders<HorseSkin>.IndexKeys.Ascending(x => x.SkinKey),
                    new CreateIndexOptions { Unique = true }
                )
            );

            var actionLogsCol = db.GetCollection<ActionLog>("action_logs");
            await actionLogsCol.Indexes.CreateOneAsync(
                new CreateIndexModel<ActionLog>(
                    Builders<ActionLog>.IndexKeys.Descending(x => x.CreatedAt)
                )
            );

            await actionLogRepo.LogAsync($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] [SYSTEM_STARTUP] 3D Co Ca Ngua Server initialized. Connected to MongoDB database '{db.DatabaseNamespace.DatabaseName}'.");

            // 1. Seed Users (All share password: 123456789)
            if (await userRepo.CountAsync() == 0)
            {
                logger.LogInformation("[MongoDB] Seeding initial users (admin, player1, player2)...");
                string defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("123456789");

                var admin = new User
                {
                    Username = "admin",
                    Password = defaultPasswordHash,
                    Role = "ADMIN",
                    FullName = "Administrator",
                    Coins = 100000
                };
                await userRepo.CreateAsync(admin);

                var player1 = new User
                {
                    Username = "player1",
                    Password = defaultPasswordHash,
                    Role = "PLAYER",
                    FullName = "Player One",
                    Coins = 10000
                };
                await userRepo.CreateAsync(player1);

                var player2 = new User
                {
                    Username = "player2",
                    Password = defaultPasswordHash,
                    Role = "PLAYER",
                    FullName = "Player Two",
                    Coins = 10000
                };
                await userRepo.CreateAsync(player2);
            }

            // 2. Seed Default Arenas (Đấu trường cờ cá ngựa)
            var existingArenas = await arenaRepo.GetAllAsync();
            Arena royalArena;
            if (existingArenas.Count == 0)
            {
                logger.LogInformation("[MongoDB] Seeding initial arenas (Đấu trường Hoàng Gia, Đấu trường Không Gian)...");
                var adminUser = await userRepo.GetByUsernameAsync("admin");
                string adminId = adminUser?.Id ?? ObjectId.GenerateNewId().ToString();

                royalArena = new Arena
                {
                    ArenaName = "Đấu trường Cờ Cá Ngựa Hoàng Gia",
                    ArenaDescription = "Bàn cờ truyền thống 4 màu kinh điển với chuồng và đường đua tiêu chuẩn",
                    Theme = "Classic",
                    LastUpdatedUserID = adminId
                };
                await arenaRepo.CreateAsync(royalArena);

                var sciFiArena = new Arena
                {
                    ArenaName = "Đấu trường Không Gian Tương Lai",
                    ArenaDescription = "Bàn cờ vũ trụ phong cách Cyberpunk với hiệu ứng ánh sáng Neon",
                    Theme = "SciFi",
                    LastUpdatedUserID = adminId
                };
                await arenaRepo.CreateAsync(sciFiArena);
            }
            else
            {
                royalArena = existingArenas[0];
            }

            // 3. Seed Horse Skins (Quân cờ / skin ngựa)
            if (await skinRepo.CountAsync() == 0)
            {
                logger.LogInformation("[MongoDB] Seeding default horse skins (Ngựa Cổ Điển, Gundam Unicorn, Odin Biến Hình, Hiệp Sĩ Arya)...");
                var initialSkins = new[]
                {
                    new HorseSkin
                    {
                        SkinKey = "default_horse",
                        SkinName = "Ngựa Gỗ Cổ Điển",
                        SkinDescription = "Quân cờ ngựa gỗ tiêu chuẩn được trang bị mặc định",
                        Price = 0,
                        Amount = 999,
                        ArenaId = royalArena.Id,
                        IsAvailable = true
                    },
                    new HorseSkin
                    {
                        SkinKey = "gundam_unicorn",
                        SkinName = "Gundam Unicorn",
                        SkinDescription = "Chiến giáp Mobile Suit Unicorn với hiệu ứng ánh sáng phát quang",
                        Price = 5000,
                        Amount = 999,
                        ArenaId = royalArena.Id,
                        IsAvailable = true
                    },
                    new HorseSkin
                    {
                        SkinKey = "odin_transformer",
                        SkinName = "Odin Biến Hình",
                        SkinDescription = "Chiến thần Odin có thể biến hình thành phi thuyền phản lực khi bay",
                        Price = 5000,
                        Amount = 999,
                        ArenaId = royalArena.Id,
                        IsAvailable = true
                    },
                    new HorseSkin
                    {
                        SkinKey = "arya",
                        SkinName = "Hiệp Sĩ Arya",
                        SkinDescription = "Nữ chiến binh Arya dũng cảm với hoạt ảnh di chuyển linh hoạt",
                        Price = 5000,
                        Amount = 999,
                        ArenaId = royalArena.Id,
                        IsAvailable = true
                    }
                };

                foreach (var skin in initialSkins)
                {
                    await skinRepo.CreateAsync(skin);
                }
            }
        }
        catch (Exception ex)
        {
            IsMongoConnected = false;
            logger.LogWarning("[Database] MongoDB connection issue: {Message}. Running in High-Speed In-Memory fallback mode.", ex.Message);
        }
    }
}
