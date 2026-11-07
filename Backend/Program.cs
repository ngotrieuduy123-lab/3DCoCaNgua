using System.Security.Claims;
using System.Text;
using CoCaNgua.Server.Models;
using CoCaNgua.Server.Repositories;
using CoCaNgua.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// 1. Logging with WebLogCollector
builder.Logging.AddProvider(new WebLogCollector());

// 2. Read Configuration
var mongoConnection = builder.Configuration["ConnectionStrings:MongoDB"] ?? "mongodb://localhost:27017";
var databaseName = builder.Configuration["DatabaseName"] ?? "GNS301_Lab_CoCaNgua";
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "GNS301_Lab_SU26_StudentCode_SecretKey_2026_SecureKeyForJwtAuth!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "GNS301_Lab";

if (jwtSecret.Length < 32)
{
    jwtSecret = jwtSecret.PadRight(32, '!');
}

// 3. Register MongoDB & Repositories
var mongoSettings = MongoClientSettings.FromConnectionString(mongoConnection);
mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
mongoSettings.ConnectTimeout = TimeSpan.FromSeconds(3);
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings));
builder.Services.AddScoped<IMongoDatabase>(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IArenaRepository, ArenaRepository>();
builder.Services.AddScoped<ISkinRepository, SkinRepository>();
builder.Services.AddScoped<IPondRepository, PondRepository>();
builder.Services.AddScoped<IFishRepository, FishRepository>();
builder.Services.AddScoped<IPurchaseLogRepository, PurchaseLogRepository>();
builder.Services.AddScoped<IActionLogRepository, ActionLogRepository>();
builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddSingleton<IJwtService, JwtService>();

// 4. Register Asynchronous TCP Broadcast Server (Port 7777)
builder.Services.AddHostedService<TcpBroadcastServer>();

// 5. Razor Pages & Antiforgery for Admin Web Dashboard
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");

// 6. Dual Authentication (Cookie for Web Dashboard, JWT for Game/API)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "SmartScheme";
    options.DefaultChallengeScheme = "SmartScheme";
})
.AddPolicyScheme("SmartScheme", "Bearer or Cookie", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }
        return CookieAuthenticationDefaults.AuthenticationScheme;
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, o =>
{
    o.LoginPath = "/Login";
    o.Cookie.Name = "CoCaNgua.AdminSession";
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("ADMIN"));
    options.AddPolicy("RequirePlayer", policy => policy.RequireRole("PLAYER", "ADMIN"));
});

// 7. Configure Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GNS301 Cờ Cá Ngựa Game API",
        Version = "v1",
        Description = "API for 3D Cờ Cá Ngựa game & GNS301 Assignment (JWT Auth, RBAC, Ponds & Fishes, TCP Broadcast)"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter JWT Bearer token: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseCors("AllowAll");
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "GNS301 API v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Health Check
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timeUtc = DateTime.UtcNow }));

#region PART 1: Authentication Endpoints
app.MapPost("/api/auth/login", async (LoginDto body, IUserRepository userRepo, IJwtService jwtService, IActionLogRepository actionLogRepo) =>
{
    if (string.IsNullOrWhiteSpace(body.username) || string.IsNullOrWhiteSpace(body.password))
        return Results.BadRequest(new { message = "Username and password are required." });

    var user = await userRepo.GetByUsernameAsync(body.username.Trim());
    if (user == null || !BCrypt.Net.BCrypt.Verify(body.password, user.Password))
        return Results.Unauthorized();

    string token = jwtService.GenerateToken(user);

    user.LastLoginUtc = DateTime.UtcNow;
    user.LastSeenUtc = DateTime.UtcNow;
    await userRepo.UpdateAsync(user);

    await actionLogRepo.LogAsync($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] [AUTH_LOGIN] Player='{user.Username}' | Role={user.Role} | Status=SUCCESS | CurrentCoins={user.Coins:N0}");

    return Results.Ok(new
    {
        token,
        role = user.Role,
        username = user.Username,
        message = "Login successful.",
        player = ToPlayerDto(user)
    });
})
.WithSummary("User login (admin / player1 / player2 - password: 123456789)")
.WithTags("Authentication");

app.MapPost("/api/auth/register", async (RegisterDto body, IUserRepository userRepo, IJwtService jwtService, IActionLogRepository actionLogRepo) =>
{
    string uname = (body.username ?? "").Trim();
    string pwd = (body.password ?? "").Trim();
    if (uname.Length < 3 || pwd.Length < 6)
        return Results.BadRequest(new { message = "Username must be at least 3 chars and password at least 6 chars." });

    var existing = await userRepo.GetByUsernameAsync(uname);
    if (existing != null)
        return Results.BadRequest(new { message = "Username already exists." });

    var user = new User
    {
        Username = uname,
        Password = BCrypt.Net.BCrypt.HashPassword(pwd),
        Role = "PLAYER",
        FullName = string.IsNullOrWhiteSpace(body.displayName) ? uname : body.displayName.Trim(),
        Coins = 1000
    };

    await userRepo.CreateAsync(user);
    string token = jwtService.GenerateToken(user);

    await actionLogRepo.LogAsync($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] [AUTH_REGISTER] Player='{user.Username}' | DisplayName='{user.FullName}' | Role={user.Role} | InitialCoins={user.Coins:N0}");

    return Results.Ok(new
    {
        token,
        role = user.Role,
        username = user.Username,
        message = "Registered successfully.",
        player = ToPlayerDto(user)
    });
})
.WithSummary("User registration")
.WithTags("Authentication");
#endregion

#region Cờ Cá Ngựa: Arena & Board Management Endpoints
app.MapGet("/api/arenas", async (IArenaRepository arenaRepo) =>
    Results.Ok(await arenaRepo.GetAllAsync()))
    .WithSummary("Get all arenas / boards (Public)")
    .WithTags("Arena Management");

app.MapGet("/api/arenas/{id}", async (string id, IArenaRepository arenaRepo) =>
{
    var arena = await arenaRepo.GetByIdAsync(id);
    return arena != null ? Results.Ok(arena) : Results.NotFound(new { message = "Arena not found" });
})
.WithSummary("Get one arena by ID (Public)")
.WithTags("Arena Management");

app.MapPost("/api/arenas", async (ArenaInputDto body, HttpContext http, IArenaRepository arenaRepo) =>
{
    string name = (body.arenaName ?? "").Trim();
    if (name.Length < 3 || name.Length > 100)
        return Results.BadRequest(new { message = "arenaName is required and must be 3-100 characters." });

    var existing = await arenaRepo.GetByNameAsync(name);
    if (existing != null)
        return Results.BadRequest(new { message = "An arena with this name already exists." });

    string? userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var arena = new Arena
    {
        ArenaName = name,
        ArenaDescription = body.arenaDescription,
        Theme = string.IsNullOrWhiteSpace(body.theme) ? "Classic" : body.theme.Trim(),
        LastUpdatedUserID = userId ?? ObjectId.GenerateNewId().ToString()
    };

    await arenaRepo.CreateAsync(arena);
    return Results.Created($"/api/arenas/{arena.Id}", arena);
})
.RequireAuthorization("RequireAdmin")
.WithSummary("Create an arena / board (Admin only)")
.WithTags("Arena Management");

app.MapPut("/api/arenas/{id}", async (string id, ArenaInputDto body, HttpContext http, IArenaRepository arenaRepo) =>
{
    var arena = await arenaRepo.GetByIdAsync(id);
    if (arena == null) return Results.NotFound(new { message = "Arena not found" });

    string name = (body.arenaName ?? "").Trim();
    if (name.Length < 3 || name.Length > 100)
        return Results.BadRequest(new { message = "arenaName is required and must be 3-100 characters." });

    string? userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    arena.ArenaName = name;
    arena.ArenaDescription = body.arenaDescription;
    if (!string.IsNullOrWhiteSpace(body.theme)) arena.Theme = body.theme.Trim();
    arena.LastUpdatedUserID = userId ?? arena.LastUpdatedUserID;

    await arenaRepo.UpdateAsync(arena);
    return Results.Ok(arena);
})
.RequireAuthorization("RequireAdmin")
.WithSummary("Update an arena (Admin only)")
.WithTags("Arena Management");
#endregion

#region Cờ Cá Ngựa: Horse Skin Management Endpoints
app.MapGet("/api/skins", async (ISkinRepository skinRepo) =>
    Results.Ok(await skinRepo.GetAllAsync()))
    .WithSummary("Get all horse skins (Public)")
    .WithTags("Skin Management");

app.MapGet("/api/skins/{id}", async (string id, ISkinRepository skinRepo) =>
{
    var skin = await skinRepo.GetBySkinKeyAsync(id) ?? await skinRepo.GetByIdAsync(id);
    return skin != null ? Results.Ok(skin) : Results.NotFound(new { message = "Skin not found" });
})
.WithSummary("Get skin by ID or SkinKey (Public)")
.WithTags("Skin Management");

app.MapPost("/api/skins", async (SkinInputDto body, ISkinRepository skinRepo) =>
{
    string key = (body.skinKey ?? "").Trim().ToLower();
    string name = (body.skinName ?? "").Trim();
    if (string.IsNullOrWhiteSpace(key) || key.Length < 2)
        return Results.BadRequest(new { message = "skinKey is required." });
    if (name.Length < 3 || name.Length > 100)
        return Results.BadRequest(new { message = "skinName must be 3-100 characters." });

    var existing = await skinRepo.GetBySkinKeyAsync(key);
    if (existing != null)
        return Results.BadRequest(new { message = "A skin with this skinKey already exists." });

    var skin = new HorseSkin
    {
        SkinKey = key,
        SkinName = name,
        SkinDescription = body.skinDescription,
        Price = Math.Max(0, body.price),
        Amount = body.amount,
        ArenaId = body.arenaId,
        IsAvailable = body.isAvailable
    };

    await skinRepo.CreateAsync(skin);
    return Results.Created($"/api/skins/{skin.Id}", skin);
})
.RequireAuthorization("RequireAdmin")
.WithSummary("Create a horse skin (Admin only)")
.WithTags("Skin Management");

app.MapPut("/api/skins/{id}", async (string id, SkinInputDto body, ISkinRepository skinRepo) =>
{
    var skin = await skinRepo.GetBySkinKeyAsync(id) ?? await skinRepo.GetByIdAsync(id);
    if (skin == null) return Results.NotFound(new { message = "Skin not found" });

    if (!string.IsNullOrWhiteSpace(body.skinName)) skin.SkinName = body.skinName.Trim();
    if (body.skinDescription != null) skin.SkinDescription = body.skinDescription;
    if (body.price >= 0) skin.Price = body.price;
    if (body.amount > 0) skin.Amount = body.amount;
    skin.IsAvailable = body.isAvailable;
    if (!string.IsNullOrEmpty(body.arenaId)) skin.ArenaId = body.arenaId;

    await skinRepo.UpdateAsync(skin);
    return Results.Ok(skin);
})
.RequireAuthorization("RequireAdmin")
.WithSummary("Update a horse skin (Admin only)")
.WithTags("Skin Management");
#endregion

#region Compatibility: Pond & Fish Endpoints
app.MapGet("/api/ponds", async (IPondRepository pondRepo) =>
    Results.Ok(await pondRepo.GetAllAsync()))
    .WithSummary("Get all ponds (Public)")
    .WithTags("Pond Management");

app.MapGet("/api/ponds/{id}", async (string id, IPondRepository pondRepo) =>
{
    var pond = await pondRepo.GetByIdAsync(id);
    return pond != null ? Results.Ok(pond) : Results.NotFound(new { message = "Pond not found" });
})
.WithSummary("Get one pond by ID (Public)")
.WithTags("Pond Management");

app.MapPost("/api/ponds", async (PondInputDto body, HttpContext http, IPondRepository pondRepo) =>
{
    string name = (body.pondName ?? "").Trim();
    if (name.Length < 3 || name.Length > 100)
        return Results.BadRequest(new { message = "pondName is required and must be 3-100 characters." });

    var existing = await pondRepo.GetByNameAsync(name);
    if (existing != null)
        return Results.BadRequest(new { message = "A pond with this name already exists." });

    string? userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var pond = new Pond
    {
        PondName = name,
        PondDescription = body.pondDescription,
        LastUpdatedUserID = userId ?? ObjectId.GenerateNewId().ToString()
    };

    await pondRepo.CreateAsync(pond);
    return Results.Created($"/api/ponds/{pond.Id}", pond);
})
.RequireAuthorization("RequireAdmin")
.WithSummary("Create a pond (Admin only)")
.WithTags("Pond Management");

app.MapPut("/api/ponds/{id}", async (string id, PondInputDto body, HttpContext http, IPondRepository pondRepo) =>
{
    var pond = await pondRepo.GetByIdAsync(id);
    if (pond == null) return Results.NotFound(new { message = "Pond not found" });

    string name = (body.pondName ?? "").Trim();
    if (name.Length < 3 || name.Length > 100)
        return Results.BadRequest(new { message = "pondName is required and must be 3-100 characters." });

    string? userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    pond.PondName = name;
    pond.PondDescription = body.pondDescription;
    pond.LastUpdatedUserID = userId ?? pond.LastUpdatedUserID;

    await pondRepo.UpdateAsync(pond);
    return Results.Ok(pond);
})
.RequireAuthorization("RequireAdmin")
.WithSummary("Update a pond (Admin only)")
.WithTags("Pond Management");
#endregion

#region PART 1: Fish Management Endpoints (Items & Skins)
app.MapGet("/api/fishes", async (IFishRepository fishRepo) =>
    Results.Ok(await fishRepo.GetAllAsync()))
    .WithSummary("Get all fishes / skins (Public)")
    .WithTags("Fish Management");

app.MapGet("/api/fishes/{id}", async (string id, IFishRepository fishRepo) =>
{
    var fish = await fishRepo.GetByIdAsync(id);
    return fish != null ? Results.Ok(fish) : Results.NotFound(new { message = "Fish not found" });
})
.WithSummary("Get fish by ID (Public)")
.WithTags("Fish Management");

app.MapPost("/api/fishes", async (FishInputDto body, IFishRepository fishRepo, IPondRepository pondRepo) =>
{
    string name = (body.fishName ?? "").Trim();
    if (name.Length < 3 || name.Length > 100)
        return Results.BadRequest(new { message = "fishName is required and must be 3-100 characters." });

    if (body.price < 0 || body.price > 99999)
        return Results.BadRequest(new { message = "price must be between 0 and 99999." });

    var existingPond = await pondRepo.GetByIdAsync(body.pondId);
    if (existingPond == null)
        return Results.BadRequest(new { message = "PondId must reference an existing Pond." });

    var fish = new Fish
    {
        FishName = name,
        FishDescription = body.fishDescription,
        Price = body.price,
        Amount = body.amount,
        PondId = body.pondId,
        IsAvailable = body.isAvailable,
        SkinKey = string.IsNullOrWhiteSpace(body.skinKey) ? name.ToLower().Replace(" ", "_") : body.skinKey
    };

    await fishRepo.CreateAsync(fish);
    return Results.Created($"/api/fishes/{fish.Id}", fish);
})
.RequireAuthorization("RequireAdmin")
.WithSummary("Create a fish / skin (Admin only)")
.WithTags("Fish Management");

app.MapPut("/api/fishes/{id}", async (string id, FishInputDto body, IFishRepository fishRepo, IPondRepository pondRepo) =>
{
    var fish = await fishRepo.GetByIdAsync(id);
    if (fish == null) return Results.NotFound(new { message = "Fish not found" });

    string name = (body.fishName ?? "").Trim();
    if (name.Length < 3 || name.Length > 100)
        return Results.BadRequest(new { message = "fishName is required and must be 3-100 characters." });

    if (body.price < 0 || body.price > 99999)
        return Results.BadRequest(new { message = "price must be between 0 and 99999." });

    if (!string.IsNullOrEmpty(body.pondId))
    {
        var existingPond = await pondRepo.GetByIdAsync(body.pondId);
        if (existingPond == null)
            return Results.BadRequest(new { message = "PondId must reference an existing Pond." });
        fish.PondId = body.pondId;
    }

    fish.FishName = name;
    fish.FishDescription = body.fishDescription;
    fish.Price = body.price;
    fish.Amount = body.amount;
    fish.IsAvailable = body.isAvailable;
    if (!string.IsNullOrWhiteSpace(body.skinKey)) fish.SkinKey = body.skinKey;

    await fishRepo.UpdateAsync(fish);
    return Results.Ok(fish);
})
.RequireAuthorization("RequireAdmin")
.WithSummary("Update a fish / skin (Admin only)")
.WithTags("Fish Management");

app.MapGet("/api/purchaselogs", async (IPurchaseLogRepository logRepo) =>
    Results.Ok(await logRepo.GetAllAsync()))
    .WithSummary("Get all purchase logs (Admin or Player)")
    .WithTags("Purchase Logs");
#endregion

#region Unity Game Specific Endpoints (Cờ Cá Ngựa compatibility)
var game = app.MapGroup("/api/game").RequireAuthorization();

game.MapGet("/me", async (HttpContext http, IUserRepository userRepo) =>
{
    string? username = http.User.FindFirst(ClaimTypes.Name)?.Value;
    var user = await userRepo.GetByUsernameAsync(username ?? "");
    if (user == null) return Results.Unauthorized();
    return Results.Ok(new { player = ToPlayerDto(user) });
});

game.MapPost("/heartbeat", async (HttpContext http, IUserRepository userRepo) =>
{
    string? username = http.User.FindFirst(ClaimTypes.Name)?.Value;
    if (!string.IsNullOrEmpty(username))
    {
        var user = await userRepo.GetByUsernameAsync(username);
        if (user != null)
        {
            user.LastSeenUtc = DateTime.UtcNow;
            await userRepo.UpdateAsync(user);
        }
    }
    return Results.Ok(new { message = "OK" });
});

game.MapGet("/skins", async (ISkinRepository skinRepo) =>
{
    var skins = await skinRepo.GetAllAsync();
    var result = skins.Select(s => new
    {
        id = string.IsNullOrEmpty(s.SkinKey) ? s.Id : s.SkinKey,
        name = s.SkinName,
        description = s.SkinDescription ?? "",
        price = (int)s.Price,
        isForSale = s.IsAvailable
    }).ToArray();
    return Results.Ok(new { skins = result });
});

game.MapPost("/skins/purchase", async (SkinPurchaseDto body, HttpContext http, IUserRepository userRepo, ISkinRepository skinRepo, IPurchaseLogRepository logRepo, IActionLogRepository actionLogRepo) =>
{
    string? username = http.User.FindFirst(ClaimTypes.Name)?.Value;
    var user = await userRepo.GetByUsernameAsync(username ?? "");
    if (user == null) return Results.Unauthorized();

    var skin = await skinRepo.GetBySkinKeyAsync(body.skinId) ?? await skinRepo.GetByIdAsync(body.skinId);
    if (skin == null) return Results.NotFound(new { message = "Skin not found." });

    int price = (int)skin.Price;
    if (user.Coins < price)
        return Results.BadRequest(new { message = "Not enough coins." });

    if (!user.OwnedSkinIds.Contains(body.skinId))
    {
        user.OwnedSkinIds.Add(body.skinId);
    }
    user.Coins -= price;
    await userRepo.UpdateAsync(user);

    var log = new PurchaseLog
    {
        PlayerId = user.Username,
        ItemId = body.skinId,
        SkinKey = skin.SkinKey,
        SkinName = skin.SkinName,
        Price = price,
        Status = "RECEIVED",
        CreatedAt = DateTime.UtcNow
    };
    await logRepo.CreateAsync(log);

    // Save plaintext action log into action_logs in MongoDB!
    string plaintextLog = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] [PURCHASE] Player='{user.Username}' | Skin='{skin.SkinName}' (Key: {skin.SkinKey}) | PricePaid={price:N0} Coins | RemainingCoins={user.Coins:N0} | Status=SUCCESS | Description='{skin.SkinDescription}'";
    await actionLogRepo.LogAsync(plaintextLog);

    return Results.Ok(new
    {
        player = ToPlayerDto(user),
        message = $"Đã mua thành công skin {skin.SkinName}!"
    });
});

game.MapPost("/skins/equip", async (SkinPurchaseDto body, HttpContext http, IUserRepository userRepo, IActionLogRepository actionLogRepo) =>
{
    string? username = http.User.FindFirst(ClaimTypes.Name)?.Value;
    var user = await userRepo.GetByUsernameAsync(username ?? "");
    if (user == null) return Results.Unauthorized();

    if (!user.OwnedSkinIds.Contains(body.skinId) && body.skinId != "default_horse")
        return Results.BadRequest(new { message = "Skin not owned." });

    user.EquippedSkinId = body.skinId;
    await userRepo.UpdateAsync(user);

    await actionLogRepo.LogAsync($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] [EQUIP_SKIN] Player='{user.Username}' | EquippedSkin='{body.skinId}'");

    return Results.Ok(new
    {
        player = ToPlayerDto(user),
        message = "Skin equipped."
    });
});

game.MapPost("/rooms", async (RoomDto body, HttpContext http, IGameRepository gameRepo, IUserRepository userRepo) =>
{
    string? username = http.User.FindFirst(ClaimTypes.Name)?.Value;
    var user = await userRepo.GetByUsernameAsync(username ?? "");
    if (user == null) return Results.Unauthorized();

    var room = await gameRepo.CreateRoomAsync(user.Id, body.code);
    return Results.Ok(new { room = new { id = room.Id } });
});

game.MapPost("/rooms/join", async (RoomDto body, HttpContext http, IGameRepository gameRepo, IUserRepository userRepo) =>
{
    string? username = http.User.FindFirst(ClaimTypes.Name)?.Value;
    var user = await userRepo.GetByUsernameAsync(username ?? "");
    if (user == null) return Results.Unauthorized();

    var room = await gameRepo.JoinRoomAsync(user.Id, body.code);
    if (room == null) return Results.NotFound(new { message = "Room not found." });
    return Results.Ok(new { room = new { id = room.Id } });
});

game.MapPost("/rooms/leave", async (HttpContext http, IGameRepository gameRepo, IUserRepository userRepo) =>
{
    string? username = http.User.FindFirst(ClaimTypes.Name)?.Value;
    var user = await userRepo.GetByUsernameAsync(username ?? "");
    if (user != null) await gameRepo.LeaveAllRoomsAsync(user.Id);
    return Results.Ok(new { message = "Left room." });
});

game.MapPost("/rooms/{id}/heartbeat", async (string id, HttpContext http, IGameRepository gameRepo, IUserRepository userRepo) =>
{
    string? username = http.User.FindFirst(ClaimTypes.Name)?.Value;
    var user = await userRepo.GetByUsernameAsync(username ?? "");
    if (user == null) return Results.Unauthorized();

    bool ok = await gameRepo.UpdateRoomHeartbeatAsync(id, user.Id);
    if (!ok) return Results.NotFound(new { message = "Room expired." });
    return Results.Ok(new { message = "OK" });
});

game.MapPost("/rooms/{id}/matches", async (string id, StartMatchDto body, HttpContext http, IGameRepository gameRepo, IUserRepository userRepo) =>
{
    string? username = http.User.FindFirst(ClaimTypes.Name)?.Value;
    var user = await userRepo.GetByUsernameAsync(username ?? "");
    if (user == null) return Results.Unauthorized();

    var match = await gameRepo.StartMatchAsync(user.Id, id, body.playerIds ?? Array.Empty<string>());
    return Results.Ok(new { match = new { id = match.Id } });
});

game.MapPost("/matches/{id}/rolls", async (string id, RollDto body, HttpContext http, IGameRepository gameRepo, IUserRepository userRepo) =>
{
    string? username = http.User.FindFirst(ClaimTypes.Name)?.Value;
    var user = await userRepo.GetByUsernameAsync(username ?? "");

    var roll = new DiceRoll
    {
        MatchId = id,
        PlayerId = user?.Id ?? "",
        DisplayName = user?.FullName ?? username ?? "",
        Slot = body.slot,
        Sequence = body.sequence,
        Dice1 = body.dice1,
        Dice2 = body.dice2,
        Total = body.dice1 + body.dice2
    };
    await gameRepo.RecordRollAsync(roll);
    return Results.Ok(new { message = "Recorded." });
});

game.MapPost("/matches/{id}/finish", async (string id, FinishMatchDto body, IGameRepository gameRepo) =>
{
    string? winnerId = null;
    if (body.ranking != null && body.ranking.Length > 0)
    {
        var match = await gameRepo.GetMatchByIdAsync(id);
        if (match != null && match.Participants.Count > body.ranking[0])
        {
            winnerId = match.Participants[body.ranking[0]].PlayerId;
        }
    }

    await gameRepo.FinishMatchAsync(id, winnerId, 1000);
    return Results.Ok(new { message = "Match saved." });
});

game.MapPost("/logout", () => Results.Ok(new { message = "Logged out." }));
#endregion

#region Web Admin Dashboard Endpoints (/api/admin)
var admin = app.MapGroup("/api/admin").RequireAuthorization("RequireAdmin");

// 1. Overview Dashboard
admin.MapGet("/overview", async (IUserRepository userRepo, IPondRepository pondRepo, IFishRepository fishRepo) =>
{
    var users = await userRepo.GetAllAsync();
    var activeCutoff = DateTime.UtcNow.AddSeconds(-90);
    int onlineCount = users.Count(u => !u.IsBanned && u.LastSeenUtc >= activeCutoff);

    return Results.Ok(new
    {
        database = DbInitializer.IsMongoConnected ? "Connected" : "In-Memory Fallback",
        databaseLatencyMs = DbInitializer.IsMongoConnected ? 2 : 0,
        players = users.Count,
        onlinePlayers = onlineCount,
        newPlayersToday = users.Count(u => u.CreatedAtUtc >= DateTime.UtcNow.Date),
        matchesToday = 0,
        activeRooms = 0,
        rollsToday = 0,
        activity = new
        {
            uptimeSeconds = (int)(DateTime.UtcNow - DateTime.Today).TotalSeconds,
            p95Ms = 1,
            failures = 0,
            requests = 1
        }
    });
});

// 2. Live Logs History (Recent 200 logs on page load)
admin.MapGet("/logs/history", () => Results.Ok(new
{
    logs = WebLogCollector.GetRecentLogs().Select(l => new
    {
        time = l.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff"),
        level = l.Level.ToString(),
        category = l.Category,
        message = l.Message
    })
}));

// 3. Live Logs Streaming via Server-Sent Events (SSE)
admin.MapGet("/logs/stream", async (HttpContext context, CancellationToken ct) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    var channel = System.Threading.Channels.Channel.CreateUnbounded<WebLogEntry>();
    Action<WebLogEntry> handler = entry => channel.Writer.TryWrite(entry);

    WebLogCollector.OnLogReceived += handler;
    try
    {
        while (!ct.IsCancellationRequested)
        {
            var log = await channel.Reader.ReadAsync(ct);
            string json = System.Text.Json.JsonSerializer.Serialize(new
            {
                time = log.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff"),
                level = log.Level.ToString(),
                category = log.Category,
                message = log.Message
            });
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
        }
    }
    catch (OperationCanceledException) { }
    finally
    {
        WebLogCollector.OnLogReceived -= handler;
    }
});

// 4. Players List
admin.MapGet("/players", async (IUserRepository userRepo) =>
{
    var users = await userRepo.GetAllAsync();
    return Results.Ok(new
    {
        items = users.Select(u => new
        {
            id = u.Id,
            username = u.Username,
            displayName = u.FullName,
            role = u.Role,
            coins = u.Coins,
            equippedSkinId = u.EquippedSkinId,
            ownedSkinIds = u.OwnedSkinIds,
            isBanned = u.IsBanned,
            lastLoginUtc = u.LastLoginUtc,
            lastSeenUtc = u.LastSeenUtc,
            createdAtUtc = u.CreatedAtUtc
        }),
        total = users.Count
    });
});

// 5. Player Ban / Unban
admin.MapPost("/players/{id}/ban", async (string id, BanDto body, IUserRepository userRepo, IActionLogRepository actionLogRepo) =>
{
    var user = await userRepo.GetByIdAsync(id);
    if (user == null) return Results.NotFound(new { message = "User not found." });

    user.IsBanned = body.isBanned;
    await userRepo.UpdateAsync(user);

    WebLogCollector.AddLog(LogLevel.Information, "Admin", $"User '{user.Username}' was {(user.IsBanned ? "BANNED" : "UNBANNED")} by Admin.");
    await actionLogRepo.LogAsync($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] [ADMIN_BAN] Player='{user.Username}' was {(user.IsBanned ? "BANNED" : "UNBANNED")} by Admin.");

    return Results.Ok(new { message = user.IsBanned ? "Đã khóa tài khoản thành công." : "Đã mở khóa tài khoản thành công." });
});

// 6. Player Edit (GM Tool: Sửa Coins, Thêm/Bớt Skin, Trang bị Skin)
admin.MapPost("/players/{id}/edit", async (string id, EditPlayerDto body, IUserRepository userRepo, IActionLogRepository actionLogRepo) =>
{
    var user = await userRepo.GetByIdAsync(id);
    if (user == null) return Results.NotFound(new { message = "User not found." });

    if (body.coins.HasValue) user.Coins = Math.Max(0, body.coins.Value);
    if (body.ownedSkinIds != null) user.OwnedSkinIds = body.ownedSkinIds.Distinct().ToList();
    if (!string.IsNullOrEmpty(body.equippedSkinId)) user.EquippedSkinId = body.equippedSkinId;
    if (body.isBanned.HasValue) user.IsBanned = body.isBanned.Value;
    if (!string.IsNullOrWhiteSpace(body.fullName)) user.FullName = body.fullName.Trim();

    await userRepo.UpdateAsync(user);

    WebLogCollector.AddLog(LogLevel.Information, "GMTool", $"[GM Tool] Updated Player '{user.Username}': Coins={user.Coins}, Equipped={user.EquippedSkinId}, Skins=[{string.Join(", ", user.OwnedSkinIds)}]");
    await actionLogRepo.LogAsync($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] [GM_TOOL_EDIT] Modified Player='{user.Username}' | Coins={user.Coins:N0} | Equipped='{user.EquippedSkinId}' | Skins=[{string.Join(", ", user.OwnedSkinIds)}] | IsBanned={user.IsBanned}");

    return Results.Ok(new
    {
        message = "Đã lưu thay đổi thông tin người chơi!",
        player = ToPlayerDto(user)
    });
});

// 7. Skins Management
admin.MapGet("/skins", async (ISkinRepository skinRepo) =>
{
    var skins = await skinRepo.GetAllAsync();
    return Results.Ok(new
    {
        items = skins.Select(s => new
        {
            id = string.IsNullOrEmpty(s.SkinKey) ? s.Id : s.SkinKey,
            name = s.SkinName,
            description = s.SkinDescription ?? "",
            price = (int)s.Price,
            isForSale = s.IsAvailable
        })
    });
});

admin.MapPost("/skins/{id}", async (string id, EditSkinDto body, ISkinRepository skinRepo, IActionLogRepository actionLogRepo) =>
{
    var skin = await skinRepo.GetBySkinKeyAsync(id) ?? await skinRepo.GetByIdAsync(id);
    if (skin == null) return Results.NotFound(new { message = "Skin not found." });

    skin.SkinName = body.name ?? skin.SkinName;
    skin.Price = body.price;
    skin.IsAvailable = body.isForSale;

    await skinRepo.UpdateAsync(skin);
    WebLogCollector.AddLog(LogLevel.Information, "Admin", $"Skin '{skin.SkinName}' was updated by Admin: Price={skin.Price}, ForSale={skin.IsAvailable}");
    await actionLogRepo.LogAsync($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] [ADMIN_UPDATE_SKIN] Skin='{skin.SkinName}' (Key: {skin.SkinKey}) | NewPrice={skin.Price:N0} Coins | IsForSale={skin.IsAvailable}");

    return Results.Ok(new { message = "Đã cập nhật skin thành công!" });
});

// 8. Action Logs Management (Plaintext logs in MongoDB)
admin.MapGet("/actionlogs", async (IActionLogRepository actionLogRepo) =>
{
    var logs = await actionLogRepo.GetAllAsync(100);
    return Results.Ok(new { items = logs });
});

app.MapGet("/api/actionlogs", async (IActionLogRepository actionLogRepo) =>
    Results.Ok(await actionLogRepo.GetAllAsync(100)))
    .WithSummary("Get recent plaintext action logs from MongoDB (action_logs collection)")
    .WithTags("Action Logs");

// 8. Placeholders for rooms, matches, rolls, transactions
admin.MapGet("/rooms", () => Results.Ok(new { items = Array.Empty<object>(), total = 0 }));
admin.MapGet("/matches", () => Results.Ok(new { items = Array.Empty<object>(), total = 0 }));
admin.MapGet("/rolls", () => Results.Ok(new { items = Array.Empty<object>(), total = 0 }));
admin.MapGet("/transactions", async (IPurchaseLogRepository logRepo) =>
{
    var logs = await logRepo.GetAllAsync();
    return Results.Ok(new
    {
        items = logs.Select(l => new
        {
            id = l.Id,
            playerId = l.PlayerId,
            kind = "Mua skin",
            amount = -(int)l.Price,
            before = 0,
            after = 0,
            reference = $"{l.FishName} ({l.Status})",
            createdAtUtc = l.CreatedAt
        }),
        total = logs.Count
    });
});
#endregion

// Initialize Database on Startup
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider, app.Logger);
}

Console.WriteLine("==================================================================");
Console.WriteLine("  GNS301 3D Cờ Cá Ngựa Backend Server Started!");
Console.WriteLine("  HTTP REST API & Swagger UI: http://localhost:5001/swagger");
Console.WriteLine("  Web Admin Dashboard:       http://localhost:5001/");
Console.WriteLine("  Asynchronous TCP Server:   Port 7777");
Console.WriteLine("==================================================================");

static object ToPlayerDto(User u) => DtoHelper.ToPlayerDto(u);

await app.RunAsync();

// DTO Records
public record LoginDto(string username, string password);
public record RegisterDto(string username, string password, string? displayName);
public record ArenaInputDto(string arenaName, string? arenaDescription, string? theme);
public record SkinInputDto(string skinKey, string skinName, string? skinDescription, double price, int amount = 999, string arenaId = "", bool isAvailable = true);
public record PondInputDto(string pondName, string? pondDescription);
public record FishInputDto(string fishName, string? fishDescription, double price, int amount, string pondId, bool isAvailable = true, string? skinKey = null);
public record SkinPurchaseDto(string skinId, int expectedPrice = -1);
public record RoomDto(string code);
public record StartMatchDto(string[] playerIds);
public record RollDto(string eventId, int sequence, int slot, int dice1, int dice2);
public record FinishMatchDto(int[] ranking, string? reason);
public record BanDto(bool isBanned);
public record EditSkinDto(string name, int price, bool isForSale);
public record EditPlayerDto(string? fullName, int? coins, string? equippedSkinId, string[]? ownedSkinIds, bool? isBanned);

public static class DtoHelper
{
    public static object ToPlayerDto(User u) => new
    {
        id = u.Id,
        username = u.Username,
        displayName = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName,
        coins = u.Coins,
        ownedSkinIds = u.OwnedSkinIds.ToArray(),
        equippedSkinId = u.EquippedSkinId
    };
}
