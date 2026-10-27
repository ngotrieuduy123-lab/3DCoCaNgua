using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;

// Integration tests use a new isolated database and remove only that database in finally.
string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
string backend = Path.Combine(root, "Backend");
var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(backend, "appsettings.Local.json")));
string uri = Environment.GetEnvironmentVariable("COCANGUA_TEST_MONGO_URI") ?? config.RootElement.GetProperty("Mongo").GetProperty("ConnectionString").GetString()!;
string databaseName = "CoCaNgua_Test_" + Guid.NewGuid().ToString("N")[..12];
var mongo = new MongoClient(uri);
var database = mongo.GetDatabase(databaseName);
var players = database.GetCollection<BsonDocument>("Players");
string adminPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
var start = new ProcessStartInfo("dotnet", '"' + Path.Combine(backend, "bin/Debug/net10.0/CoCaNgua.Server.dll") + '"') { WorkingDirectory = backend, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
start.Environment["Mongo__ConnectionString"] = uri;
start.Environment["Mongo__Database"] = databaseName;
start.Environment["Admin__Username"] = "testadmin";
start.Environment["Admin__Password"] = adminPassword;
start.Environment["Urls"] = "http://127.0.0.1:5081";
using var server = Process.Start(start)!;
server.OutputDataReceived += (_, _) => { };
server.ErrorDataReceived += (_, _) => { };
server.BeginOutputReadLine(); server.BeginErrorReadLine();
using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5081"), Timeout = TimeSpan.FromSeconds(30) };
int checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception("FAILED: " + label); checks++; Console.WriteLine("PASS " + label); }
async Task<JsonElement> Call(string path, object? body = null, string? token = null, int expected = 200, HttpClient? client = null, string? csrf = null)
{
    using var request = new HttpRequestMessage(body == null ? HttpMethod.Get : HttpMethod.Post, path);
    if (body != null) request.Content = JsonContent.Create(body);
    if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    if (csrf != null) request.Headers.Add("X-CSRF-TOKEN", csrf);
    using var response = await (client ?? http).SendAsync(request);
    string text = await response.Content.ReadAsStringAsync();
    Check((int)response.StatusCode == expected, path + " status " + (int)response.StatusCode + " expected " + expected);
    return string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
}
try
{
    bool ready = false;
    for (int i = 0; i < 60; i++)
    {
        if (server.HasExited) throw new Exception("Backend exited during initialization.");
        try { ready = (await http.GetAsync("/health")).IsSuccessStatusCode; } catch (HttpRequestException) { }
        if (ready) break;
        await Task.Delay(500);
    }
    Check(ready, "Backend starts with a real MongoDB connection");
    await Call("/api/game/me", expected: 401);
    await Call("/api/admin/players", expected: 401);
    var host = await Call("/api/auth/register", new { username = "host", password = "HostPassword!123", displayName = "Host" });
    var guest = await Call("/api/auth/register", new { username = "guest", password = "GuestPassword!123", displayName = "Guest" });
    var outsider = await Call("/api/auth/register", new { username = "outsider", password = "OtherPassword!123", displayName = "Other" });
    string h = host.GetProperty("token").GetString()!, g = guest.GetProperty("token").GetString()!, o = outsider.GetProperty("token").GetString()!;
    string hid = host.GetProperty("player").GetProperty("id").GetString()!, gid = guest.GetProperty("player").GetProperty("id").GetString()!, oid = outsider.GetProperty("player").GetProperty("id").GetString()!;
    Check(!host.GetRawText().Contains("password", StringComparison.OrdinalIgnoreCase) && !host.GetRawText().Contains("credential", StringComparison.OrdinalIgnoreCase), "Auth response contains no password material");
    Check(host.GetProperty("player").GetProperty("coins").GetInt32() == 1000, "Signup credits 1000 coins");
    await Call("/api/auth/login", new { username = "host", password = "WrongPassword", displayName = "" }, expected: 401);
    await Call("/api/auth/register", new { username = "host", password = "HostPassword!123", displayName = "Host" }, expected: 409);
    await Call("/api/game/skins/purchase", new { skinId = "gundam_unicorn", expectedPrice = 5000 }, g, 409);
    await players.UpdateOneAsync(new BsonDocument("_id", ObjectId.Parse(hid)), new BsonDocument("$set", new BsonDocument("coins", 6000)));
    await Call("/api/game/skins/purchase", new { skinId = "gundam_unicorn", expectedPrice = 1 }, h, 409);
    var purchases = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/game/skins/purchase") { Content = JsonContent.Create(new { skinId = "gundam_unicorn", expectedPrice = 5000 }) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", h);
        using var response = await http.SendAsync(req); return (int)response.StatusCode;
    }));
    Check(purchases.Count(x => x == 200) == 1 && purchases.Count(x => x == 409) == 1, "Concurrent purchase charges exactly once");
    var me = await Call("/api/game/me", token: h);
    Check(me.GetProperty("player").GetProperty("coins").GetInt32() == 1000, "Purchase balance is correct");
    await Call("/api/game/skins/equip", new { skinId = "gundam_unicorn", expectedPrice = 5000 }, g, 403);
    await Call("/api/game/skins/equip", new { skinId = "gundam_unicorn", expectedPrice = 5000 }, h);
    var room = await Call("/api/game/rooms", new { code = "TEST01" }, h);
    string rid = room.GetProperty("room").GetProperty("id").GetString()!;
    await Call($"/api/game/rooms/{rid}/matches", new { playerIds = new[] { hid, gid } }, h, 409);
    await Call("/api/game/rooms/join", new { code = "TEST01" }, g);
    await Call($"/api/game/rooms/{rid}/matches", new { playerIds = new[] { oid, gid } }, o, 403);
    var match = await Call($"/api/game/rooms/{rid}/matches", new { playerIds = new[] { hid, gid } }, h);
    string mid = match.GetProperty("match").GetProperty("id").GetString()!;
    var repeatedStart = await Call($"/api/game/rooms/{rid}/matches", new { playerIds = new[] { hid, gid } }, h);
    Check(repeatedStart.GetProperty("match").GetProperty("id").GetString() == mid, "Retried match start returns the same match");
    var roll = new { eventId = Guid.NewGuid().ToString(), sequence = 1, slot = 1, dice1 = 2, dice2 = 6 };
    await Call($"/api/game/matches/{mid}/rolls", roll, g, 403);
    await Call($"/api/game/matches/{mid}/rolls", new { eventId = Guid.NewGuid().ToString(), sequence = 1, slot = 1, dice1 = 0, dice2 = 7 }, h, 400);
    await Task.WhenAll(Call($"/api/game/matches/{mid}/rolls", roll, h), Call($"/api/game/matches/{mid}/rolls", roll, h));
    await Call($"/api/game/matches/{mid}/rolls", new { eventId = Guid.NewGuid().ToString(), sequence = 1, slot = 1, dice1 = 2, dice2 = 6 }, h, 409);
    await Call($"/api/game/matches/{mid}/rolls", new { roll.eventId, roll.sequence, roll.slot, dice1 = 3, dice2 = 6 }, h, 409);
    Check(await database.GetCollection<BsonDocument>("DiceRolls").CountDocumentsAsync(new BsonDocument("matchId", mid)) == 1, "Retried dice event is stored once");
    await Call($"/api/game/matches/{mid}/finish", new { ranking = new[] { 0, 0 }, reason = "test" }, h, 400);
    await Call($"/api/game/matches/{mid}/finish", new { ranking = new[] { 0, 1 }, reason = "test" }, g, 403);
    await Task.WhenAll(Call($"/api/game/matches/{mid}/finish", new { ranking = new[] { 0, 1 }, reason = "test" }, h), Call($"/api/game/matches/{mid}/finish", new { ranking = new[] { 0, 1 }, reason = "test" }, h));
    me = await Call("/api/game/me", token: h);
    Check(me.GetProperty("player").GetProperty("coins").GetInt32() == 2000, "Concurrent finish grants one 1000-coin reward");
    Check(await database.GetCollection<BsonDocument>("CoinTransactions").CountDocumentsAsync(new BsonDocument("_id", "win:" + mid)) == 1, "Reward ledger contains exactly one entry");
    await Call($"/api/game/matches/{mid}/rolls", new { eventId = Guid.NewGuid().ToString(), sequence = 2, slot = 0, dice1 = 1, dice2 = 1 }, h, 409);
    var secondRoom = await Call("/api/game/rooms", new { code = "TEST02" }, h);
    string secondRoomId = secondRoom.GetProperty("room").GetProperty("id").GetString()!;
    await database.GetCollection<BsonDocument>("Rooms").UpdateOneAsync(new BsonDocument("_id", secondRoomId), new BsonDocument("$set", new BsonDocument("lastSeenUtc", DateTime.UtcNow.AddMinutes(-5))));
    await Call("/api/game/rooms/join", new { code = "TEST02" }, g, 404);
    await Call($"/api/game/rooms/{secondRoomId}/heartbeat", new { }, h);
    await Call("/api/game/rooms/join", new { code = "TEST02" }, g);
    var secondMatch = await Call($"/api/game/rooms/{secondRoomId}/matches", new { playerIds = new[] { hid, gid } }, h);
    string abortedId = secondMatch.GetProperty("match").GetProperty("id").GetString()!;
    var aborted = await Call($"/api/game/matches/{abortedId}/finish", new { ranking = Array.Empty<int>(), reason = "Host stopped the room" }, h);
    Check(aborted.GetProperty("match").GetProperty("status").GetString() == "Aborted" && await database.GetCollection<BsonDocument>("CoinTransactions").CountDocumentsAsync(new BsonDocument("_id", "win:" + abortedId)) == 0, "Aborted match does not grant a reward");
    var cookieHandler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
    using var admin = new HttpClient(cookieHandler) { BaseAddress = http.BaseAddress };
    string loginHtml = await admin.GetStringAsync("/Login");
    string formToken = WebUtility.HtmlDecode(Regex.Match(loginHtml, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    Check(formToken.Length > 0, "Admin login has CSRF protection");
    using var loginResponse = await admin.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["Username"] = "testadmin", ["Password"] = adminPassword, ["__RequestVerificationToken"] = formToken }));
    Check(loginResponse.StatusCode == HttpStatusCode.Redirect, "Admin cookie login succeeds");
    string dashboardHtml = await admin.GetStringAsync("/");
    string csrf = WebUtility.HtmlDecode(Regex.Match(dashboardHtml, "name=\"csrf-token\" content=\"([^\"]+)\"").Groups[1].Value);
    Check(csrf.Length > 0 && dashboardHtml.Contains("Xúc xắc"), "Dashboard includes dice history and CSRF token");
    foreach (string area in new[] { "overview", "players", "rooms", "matches", "rolls", "transactions", "skins" }) await Call("/api/admin/" + area, client: admin);
    var rolls = await Call("/api/admin/rolls?matchId=" + mid + "&playerId=" + gid, client: admin);
    Check(rolls.GetProperty("total").GetInt64() == 1 && rolls.GetProperty("items")[0].GetProperty("total").GetInt32() == 8, "Admin filters dice history by match and player");
    await Call("/api/admin/skins/odin_transformer", new { name = "Odin test", price = 1234, isForSale = false }, expected: 400, client: admin);
    await Call("/api/admin/skins/odin_transformer", new { name = "Odin test", price = 1234, isForSale = false }, client: admin, csrf: csrf);
    var skins = await Call("/api/game/skins", token: h);
    Check(!skins.GetRawText().Contains("preview", StringComparison.OrdinalIgnoreCase) && skins.GetRawText().Contains("Odin test"), "Skin API serves names/settings without preview media");
    await Call("/api/game/skins/purchase", new { skinId = "odin_transformer", expectedPrice = 5000 }, h, 404);
    await Call("/api/admin/players/" + gid + "/ban", new { isBanned = true }, client: admin, csrf: csrf);
    await Call("/api/game/me", token: g, expected: 401);
    await Call("/api/admin/players/" + gid + "/ban", new { isBanned = false }, client: admin, csrf: csrf);
    await Call("/api/game/me", token: g, expected: 401);
    await Call("/api/auth/login", new { username = "guest", password = "GuestPassword!123", displayName = "" });
    byte[] salt = RandomNumberGenerator.GetBytes(16);
    using var hmac = new HMACSHA256(salt);
    var legacyId = ObjectId.GenerateNewId();
    await players.InsertOneAsync(new BsonDocument { ["_id"] = legacyId, ["username"] = "legacy", ["displayName"] = "Legacy", ["coins"] = 6000, ["ownedSkinIds"] = new BsonArray { "default_horse" }, ["equippedSkinId"] = "retired_test_skin", ["passwordSalt"] = Convert.ToBase64String(salt), ["passwordHash"] = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes("LegacyPassword!123"))) });
    var legacy = await Call("/api/auth/login", new { username = "legacy", password = "LegacyPassword!123", displayName = "" });
    var migrated = await players.Find(new BsonDocument("_id", legacyId)).FirstAsync();
    Check(migrated.Contains("credentialHash") && !migrated.Contains("passwordHash") && !migrated.Contains("passwordSalt") && migrated["equippedSkinId"] == "default_horse", "Legacy password migrates and removed skin falls back safely");
    await Call("/api/game/skins/purchase", new { skinId = "gundam_unicorn", expectedPrice = 5000 }, legacy.GetProperty("token").GetString());
    string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(o)));
    await database.GetCollection<BsonDocument>("BackendSessions").UpdateOneAsync(new BsonDocument("_id", hash), new BsonDocument("$set", new BsonDocument("expiresAtUtc", DateTime.UtcNow.AddMinutes(-1))));
    await Call("/api/game/me", token: o, expected: 401);
    var catalog = await Call("/api/game/skins", token: h);
    Check(catalog.GetProperty("skins").EnumerateArray().Any(s => s.GetProperty("id").GetString() == "arya" && s.GetProperty("name").GetString() == "Arya" && s.GetProperty("price").GetInt32() == 5000 && s.GetProperty("isForSale").GetBoolean()), "Arya is available by name for 5000 coins");
    await players.UpdateOneAsync(new BsonDocument("_id", ObjectId.Parse(hid)), new BsonDocument("$set", new BsonDocument("coins", 6000)));
    await Call("/api/game/skins/purchase", new { skinId = "arya", expectedPrice = 5000 }, h);
    var equippedArya = await Call("/api/game/skins/equip", new { skinId = "arya" }, h);
    Check(equippedArya.GetProperty("player").GetProperty("equippedSkinId").GetString() == "arya" && equippedArya.GetProperty("player").GetProperty("coins").GetInt32() == 1000, "Arya purchase and equip use the real backend economy");
    Console.WriteLine($"ALL {checks} CHECKS PASSED");
}
finally
{
    if (!server.HasExited) { server.Kill(entireProcessTree: true); await server.WaitForExitAsync(); }
    if (!databaseName.StartsWith("CoCaNgua_Test_", StringComparison.Ordinal) || databaseName == "LudoGameDB") throw new Exception("Refusing unsafe test cleanup.");
    await mongo.DropDatabaseAsync(databaseName);
    Console.WriteLine("Isolated integration-test database removed.");
}
