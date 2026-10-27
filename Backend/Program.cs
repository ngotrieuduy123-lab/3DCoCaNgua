using CoCaNgua.Server;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Match = CoCaNgua.Server.Match;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Services.AddSingleton<Store>();
builder.Services.AddSingleton<ServerActivity>();
builder.Services.AddHostedService<RoomExpiryService>();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".keys")));
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => {
    o.LoginPath = "/Login";
    o.Cookie.Name = "CoCaNgua.Admin";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.Events.OnRedirectToLogin = c => {
        if (c.Request.Path.StartsWithSegments("/api")) c.Response.StatusCode = 401;
        else c.Response.Redirect(c.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("game", context => RateLimitPartition.GetFixedWindowLimiter(context.Request.Headers.Authorization.ToString(), _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 32 * 1024);
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.Use(async (context, next) => {
    var clock = Stopwatch.StartNew();
    try { await next(context); }
    catch (Exception error) when (!context.Response.HasStarted)
    {
        int status = error is ApiError api ? api.Status : error is BadHttpRequestException ? 400 : error is AntiforgeryValidationException ? 400 : 503;
        context.Response.StatusCode = status;
        string message = error is ApiError known ? known.Message : status == 400 ? "Invalid request." : "Service temporarily unavailable. Please retry.";
        if (status >= 500) context.RequestServices.GetRequiredService<ServerActivity>().Error(context.Request.Path, error.GetType().Name);
        await context.Response.WriteAsJsonAsync(new { message });
    }
    finally { context.RequestServices.GetRequiredService<ServerActivity>().Request(clock.Elapsed.TotalMilliseconds, context.Response.StatusCode); }
});
app.Use(async (context, next) => {
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path == "/" || context.Request.Path == "/Login") context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapGet("/health", async (Store store) => {
    await store.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
    return Results.Ok(new { status = "Healthy", database = "Connected", utc = DateTime.UtcNow });
});
app.MapPost("/api/auth/register", async (Credentials body, Store store) => Results.Ok(await store.Login(body, true))).RequireRateLimiting("auth");
app.MapPost("/api/auth/login", async (Credentials body, Store store) => Results.Ok(await store.Login(body, false))).RequireRateLimiting("auth");

var game = app.MapGroup("/api/game").RequireRateLimiting("game");
game.AddEndpointFilter(async (context, next) => {
    var http = context.HttpContext;
    string header = http.Request.Headers.Authorization.ToString();
    var player = header.StartsWith("Bearer ", StringComparison.Ordinal) ? await http.RequestServices.GetRequiredService<Store>().Authenticate(header[7..]) : null;
    if (player == null) return Results.Json(new { message = "Session expired or account locked. Please log in again." }, statusCode: 401);
    http.Items["player"] = player;
    return await next(context);
});
static Player Current(HttpContext http) => (Player)http.Items["player"]!;
static void ValidId(string id) { if (!ObjectId.TryParse(id, out _)) throw new ApiError(400, "Invalid ID."); }
game.MapGet("/me", (HttpContext http) => Results.Ok(new { player = Store.View(Current(http)) }));
game.MapPost("/heartbeat", async (HttpContext http, Store store) => {
    string playerId = Current(http).Id;
    await store.Players.UpdateOneAsync(x => x.Id == playerId, Builders<Player>.Update.Set(x => x.LastSeenUtc, DateTime.UtcNow));
    return Results.Ok(new { message = "OK" });
});
game.MapPost("/logout", async (HttpContext http, Store store) => {
    string playerId = Current(http).Id;
    string token = http.Request.Headers.Authorization.ToString()[7..];
    await store.Sessions.DeleteOneAsync(x => x.Id == Store.TokenHash(token));
    await store.LeaveAllRooms(Current(http).Id);
    await store.Players.UpdateOneAsync(x => x.Id == playerId, Builders<Player>.Update.Set(x => x.LastSeenUtc, DateTime.UnixEpoch));
    return Results.Ok(new { message = "Logged out." });
});
game.MapGet("/skins", async (Store store) => Results.Ok(new { skins = await store.Skins.Find(_ => true).SortBy(x => x.Price).ToListAsync() }));
game.MapPost("/skins/purchase", async (SkinRequest body, HttpContext http, Store store) => Results.Ok(new { player = Store.View(await store.Purchase(Current(http), body.SkinId, body.ExpectedPrice)), message = "Purchased successfully." }));
game.MapPost("/skins/equip", async (SkinRequest body, HttpContext http, Store store) => Results.Ok(new { player = Store.View(await store.Equip(Current(http), body.SkinId)), message = "Skin equipped." }));
game.MapPost("/rooms", async (CreateRoomRequest body, HttpContext http, Store store) => Results.Ok(new { room = await store.CreateRoom(Current(http), body.Code) }));
game.MapPost("/rooms/join", async (JoinRoomRequest body, HttpContext http, Store store) => Results.Ok(new { room = await store.JoinRoom(Current(http), body.Code) }));
game.MapPost("/rooms/leave", async (HttpContext http, Store store) => { await store.LeaveAllRooms(Current(http).Id); return Results.Ok(new { message = "Left room." }); });
game.MapPost("/rooms/{id}/heartbeat", async (string id, HttpContext http, Store store) => {
    string playerId = Current(http).Id;
    var result = await store.Rooms.UpdateOneAsync(x => x.Id == id && x.HostPlayerId == playerId && x.Status != "Closed", Builders<Room>.Update.Set(x => x.LastSeenUtc, DateTime.UtcNow));
    if (result.MatchedCount == 0) throw new ApiError(404, "Room expired.");
    return Results.Ok(new { message = "OK" });
});
game.MapPost("/rooms/{id}/matches", async (string id, StartMatchRequest body, HttpContext http, Store store) => Results.Ok(new { match = await store.StartMatch(Current(http), id, body.PlayerIds) }));
game.MapPost("/matches/{id}/rolls", async (string id, RollRequest body, HttpContext http, Store store) => { await store.RecordRoll(Current(http), id, body); return Results.Ok(new { message = "Recorded." }); });
game.MapPost("/matches/{id}/finish", async (string id, FinishMatchRequest body, HttpContext http, Store store) => Results.Ok(new { match = await store.FinishMatch(Current(http), id, body), message = "Match saved." }));

var admin = app.MapGroup("/api/admin").RequireAuthorization();
admin.AddEndpointFilter(async (context, next) => {
    if (!HttpMethods.IsGet(context.HttpContext.Request.Method)) await context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context.HttpContext);
    return await next(context);
});
static int Page(HttpRequest request) => int.TryParse(request.Query["page"], out int page) ? Math.Clamp(page, 0, 10000) : 0;
static string Search(HttpRequest request) => request.Query["q"].ToString().Trim()[..Math.Min(80, request.Query["q"].ToString().Trim().Length)];
admin.MapGet("/overview", async (Store store, ServerActivity activity) => {
    bool dbOk;
    var timer = Stopwatch.StartNew();
    try { await store.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1)); dbOk = true; } catch { dbOk = false; }
    if (!dbOk) return Results.Ok(new { database = "Unavailable", activity = activity.Snapshot() });
    var today = DateTime.UtcNow.Date;
    return Results.Ok(new {
        database = "Connected", databaseLatencyMs = Math.Round(timer.Elapsed.TotalMilliseconds),
        players = await store.Players.CountDocumentsAsync(_ => true),
        onlinePlayers = await store.Players.CountDocumentsAsync(x => !x.IsBanned && x.LastSeenUtc > DateTime.UtcNow.AddSeconds(-90)),
        newPlayersToday = await store.Players.CountDocumentsAsync(x => x.CreatedAtUtc >= today),
        matchesToday = await store.Matches.CountDocumentsAsync(x => x.StartedAtUtc >= today),
        activeRooms = await store.Rooms.CountDocumentsAsync(x => x.Status != "Closed" && x.LastSeenUtc > DateTime.UtcNow.AddSeconds(-90)),
        rollsToday = await store.Rolls.CountDocumentsAsync(x => x.RolledAtUtc >= today),
        activity = activity.Snapshot(), utc = DateTime.UtcNow
    });
});
admin.MapGet("/players", async (HttpRequest request, Store store) => {
    string q = Search(request);
    var filter = string.IsNullOrEmpty(q) ? Builders<Player>.Filter.Empty : Builders<Player>.Filter.Regex(x => x.Username, new BsonRegularExpression(Regex.Escape(q), "i")) | Builders<Player>.Filter.Regex(x => x.DisplayName, new BsonRegularExpression(Regex.Escape(q), "i"));
    var players = await store.Players.Find(filter).SortByDescending(x => x.CreatedAtUtc).Skip(Page(request) * 30).Limit(30).ToListAsync();
    return Results.Ok(new { items = players.Select(Store.View), total = await store.Players.CountDocumentsAsync(filter) });
});
admin.MapPost("/players/{id}/ban", async (string id, BanRequest body, Store store) => {
    ValidId(id);
    var result = await store.Players.UpdateOneAsync(x => x.Id == id, Builders<Player>.Update.Set(x => x.IsBanned, body.IsBanned));
    if (result.MatchedCount == 0) throw new ApiError(404, "Player not found.");
    if (body.IsBanned) { await store.Sessions.DeleteManyAsync(x => x.PlayerId == id); await store.LeaveAllRooms(id); }
    return Results.Ok(new { message = body.IsBanned ? "Account locked and backend sessions revoked." : "Account unlocked. Player may log in again." });
});
admin.MapGet("/rooms", async (HttpRequest request, Store store) => {
    var rooms = await store.Rooms.Find(_ => true).SortByDescending(x => x.CreatedAtUtc).Skip(Page(request) * 30).Limit(30).ToListAsync();
    var ids = rooms.SelectMany(x => x.MemberIds).Distinct().ToArray();
    var players = await store.Players.Find(x => ids.Contains(x.Id)).ToListAsync();
    return Results.Ok(new { items = rooms.Select(r => new { r.Id, r.Code, r.HostPlayerId, r.Status, r.CurrentMatchId, r.CreatedAtUtc, r.LastSeenUtc,
        online = r.Status != "Closed" && r.LastSeenUtc > DateTime.UtcNow.AddSeconds(-90), members = r.MemberIds.Select(id => new { id, name = players.Find(p => p.Id == id)?.DisplayName ?? id }) }), total = await store.Rooms.CountDocumentsAsync(_ => true) });
});
admin.MapGet("/matches", async (HttpRequest request, Store store) => {
    string q = Search(request);
    var filter = Builders<Match>.Filter.Empty;
    if (!string.IsNullOrEmpty(q)) filter = ObjectId.TryParse(q, out _) ? Builders<Match>.Filter.Eq(x => x.Id, q) | Builders<Match>.Filter.Eq("participants.playerId", q) | Builders<Match>.Filter.Eq(x => x.HostPlayerId, q) : Builders<Match>.Filter.Regex(x => x.HostUsername, new BsonRegularExpression(Regex.Escape(q), "i"));
    return Results.Ok(new { items = await store.Matches.Find(filter).SortByDescending(x => x.StartedAtUtc).Skip(Page(request) * 30).Limit(30).ToListAsync(), total = await store.Matches.CountDocumentsAsync(filter) });
});
admin.MapGet("/rolls", async (HttpRequest request, Store store) => {
    var filter = Builders<DiceRoll>.Filter.Empty;
    string matchId = request.Query["matchId"].ToString();
    string playerId = request.Query["playerId"].ToString();
    if (matchId.Length > 0) filter &= Builders<DiceRoll>.Filter.Eq(x => x.MatchId, matchId);
    if (playerId.Length > 0) filter &= Builders<DiceRoll>.Filter.Eq(x => x.PlayerId, playerId);
    return Results.Ok(new { items = await store.Rolls.Find(filter).SortByDescending(x => x.RolledAtUtc).Skip(Page(request) * 50).Limit(50).ToListAsync(), total = await store.Rolls.CountDocumentsAsync(filter) });
});
admin.MapGet("/transactions", async (HttpRequest request, Store store) => {
    string q = Search(request);
    var filter = string.IsNullOrEmpty(q) ? Builders<LedgerEntry>.Filter.Empty : Builders<LedgerEntry>.Filter.Eq(x => x.PlayerId, q) | Builders<LedgerEntry>.Filter.Eq(x => x.Reference, q);
    return Results.Ok(new { items = await store.Ledger.Find(filter).SortByDescending(x => x.CreatedAtUtc).Skip(Page(request) * 30).Limit(30).ToListAsync(), total = await store.Ledger.CountDocumentsAsync(filter) });
});
admin.MapGet("/skins", async (Store store) => Results.Ok(new { items = await store.Skins.Find(_ => true).SortBy(x => x.Price).ToListAsync() }));
admin.MapPost("/skins/{id}", async (string id, EditSkinRequest body, Store store) => {
    string name = (body.Name ?? "").Trim();
    if (name.Length is < 1 or > 80 || name.Any(char.IsControl) || body.Price is < 0 or > 1000000 || (id == "default_horse" && (body.Price != 0 || !body.IsForSale))) throw new ApiError(400, "Invalid skin settings. Default horse must stay free and enabled.");
    var result = await store.Skins.UpdateOneAsync(x => x.Id == id, Builders<Skin>.Update.Set(x => x.Name, name).Set(x => x.Price, body.Price).Set(x => x.IsForSale, body.IsForSale));
    if (result.MatchedCount == 0) throw new ApiError(404, "Skin not found.");
    return Results.Ok(new { message = "Skin updated." });
});

try { await app.Services.GetRequiredService<Store>().Initialize(); }
catch (Exception e) { Console.Error.WriteLine("Backend database initialization failed (" + e.GetType().Name + "). Check local Mongo configuration and Atlas access. No credentials were logged."); return; }
if (string.IsNullOrWhiteSpace(builder.Configuration["Admin:Password"]) || builder.Configuration["Admin:Password"]!.Length < 12)
{
    Console.Error.WriteLine("Set Admin:Password to at least 12 characters in local configuration or environment.");
    return;
}
await app.RunAsync();

namespace CoCaNgua.Server
{
    public sealed class ServerActivity
    {
        readonly object gate = new();
        readonly Queue<double> durations = new();
        readonly Queue<object> errors = new();
        readonly DateTime started = DateTime.UtcNow;
        long requests, failures;
        public void Request(double ms, int status) { lock (gate) { requests++; if (status >= 500) failures++; durations.Enqueue(ms); if (durations.Count > 500) durations.Dequeue(); } }
        public void Error(string path, string type) { lock (gate) { errors.Enqueue(new { time = DateTime.UtcNow, path, type }); if (errors.Count > 20) errors.Dequeue(); } }
        public object Snapshot() { lock (gate) { var sorted = durations.Order().ToArray(); return new { startedAtUtc = started, uptimeSeconds = (int)(DateTime.UtcNow - started).TotalSeconds, requests, failures, p95Ms = sorted.Length == 0 ? 0 : Math.Round(sorted[(int)((sorted.Length - 1) * .95)]), errors = errors.Reverse().ToArray() }; } }
    }

    public sealed class RoomExpiryService(IServiceProvider services) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken token)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(token))
            {
                try
                {
                    var store = services.GetRequiredService<Store>();
                    var cutoff = DateTime.UtcNow.AddSeconds(-90);
                    var stale = await store.Rooms.Find(x => x.Status != "Closed" && x.LastSeenUtc < cutoff).Limit(100).ToListAsync(token);
                    foreach (var room in stale)
                    {
                        var result = await store.Rooms.UpdateOneAsync(x => x.Id == room.Id && x.LastSeenUtc < cutoff, Builders<Room>.Update.Set(x => x.Status, "Closed"), cancellationToken: token);
                        if (result.ModifiedCount == 0 || room.CurrentMatchId == null) continue;
                        await store.Matches.UpdateOneAsync(x => x.Id == room.CurrentMatchId && x.Status == "Playing", Builders<Match>.Update.Set(x => x.Status, "Aborted").Set(x => x.EndReason, "Host heartbeat expired").Set(x => x.EndedAtUtc, DateTime.UtcNow), cancellationToken: token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception e) { services.GetRequiredService<ServerActivity>().Error("room-expiry", e.GetType().Name); }
            }
        }
    }
}
