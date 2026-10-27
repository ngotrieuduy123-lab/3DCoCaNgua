# 3D Co Ca Ngua

Unity multiplayer Co Ca Ngua project using:

- Unity 6.3 LTS `6000.3.16f1`
- Netcode for GameObjects
- Unity Relay
- ASP.NET Core C# backend, MongoDB accounts/economy, admin dashboard and dice history

## Clone

```bash
git clone https://github.com/ngotrieuduy123-lab/3DCoCaNgua.git
```

Open the cloned folder with Unity Hub using Unity `6000.3.16f1`.

## Required Setup

1. Install Unity `6000.3.16f1` or a compatible Unity 6.3 LTS version.
2. Install the .NET 10 SDK. Configure `Backend/appsettings.Local.json` (Git-ignored):

```json
{
  "Mongo": { "ConnectionString": "YOUR_MONGODB_URI", "Database": "LudoGameDB" },
  "Admin": { "Username": "admin", "Password": "YOUR_LONG_ADMIN_PASSWORD" }
}
```

3. Run `dotnet run --project Backend/CoCaNgua.Server.csproj --launch-profile CoCaNgua`.
4. Open the admin dashboard at `http://127.0.0.1:5080`. Admin credentials are in the local configuration; password must be at least 12 characters.
5. Open Unity, let packages restore, then open `Assets/Scenes/AuthScene.unity`.
6. `DatabaseManager.Backend URL` defaults to `http://127.0.0.1:5080`; it can be overridden by `COCANGUA_BACKEND_URL`. Other machines require a reachable HTTPS deployment URL, not their own localhost.

Unity calls the backend for login, profile, purchases, equip, rooms, match results and roll events. Only the backend holds MongoDB credentials. Environment overrides: `Mongo__ConnectionString`, `Mongo__Database`, `Admin__Username`, `Admin__Password`.

For deployment, set environment `Production`, configure HTTPS and `AllowedHosts`, and provide secrets through the host environment. Persist and protect backend Data Protection keys. MongoDB must support transactions (Atlas/replica set); a standalone `mongod` is insufficient for economy transactions. Rotate any MongoDB credential previously committed to Git; removing it from current assets does not revoke the old credential or remove Git history.

## Scenes

Build Settings should include:

1. `Assets/Scenes/AuthScene.unity`
2. `Assets/Scenes/LobbyScene.unity`
3. `Assets/Scenes/GameScene.unity`

`AuthScene` is the first scene.

## Multiplayer Test

1. Player A logs in and creates a room in `LobbyScene`.
2. Player A copies/shares the room code.
3. Player B logs in, enters the room code, and joins.
4. Players click Ready.
5. Host clicks Start.
6. Roll dice and check the dashboard's dice tab. Filter by match ID or player ID. Complete a match and verify exactly one reward transaction.

The project uses Unity Relay, so router port forwarding should not be needed.

## Notes

- Do not commit MongoDB credentials.
- If login/register fails, check backend `/health`, local server configuration, and Unity's Backend URL.
- If Relay fails, check Unity Services project linking and internet access.
- Build folders and local IDE files are ignored by Git.
- Dashboard sections: overview, players/lock, rooms, matches, dice, coin transactions and skin names/prices/availability. No skin preview media is served or displayed.
- Account passwords migrate to PBKDF2 on successful legacy login. Player access tokens stay in memory; the database stores token hashes with expiry. Old match history is preserved; old dice/transaction history cannot be reconstructed.
- Backend sessions heartbeat every 20 seconds; online and host room expiry use a 90-second threshold. Banning revokes backend sessions; connected Unity clients return to login on their next API call/heartbeat.
- NGO/Relay gameplay is still host-authoritative. Dice and results are marked as host reported; the backend does not independently prove who won. This is not a competitive anti-cheat server. Roll upload is queued in memory with bounded retries; host crashes/network loss can leave missing events.
- All clients must use this updated build because lobby RPC/profile layout changed.

## Validation

Build backend: `dotnet build Backend/CoCaNgua.Server.csproj`.

Integration checks: `dotnet run --project Backend.Tests/CoCaNgua.Server.Tests.csproj`. These start a test server on port 5081, create a uniquely named isolated MongoDB database, and drop only that test database in `finally`. The configured database user needs permission to create/drop that test database. Set `COCANGUA_TEST_MONGO_URI` to test against a separate transaction-capable MongoDB deployment.
