# 3D Co Ca Ngua

Unity multiplayer Co Ca Ngua project using:

- Unity 6.3 LTS `6000.3.16f1`
- Netcode for GameObjects
- Unity Relay
- MongoDB account login/register

## Clone

```bash
git clone https://github.com/ngotrieuduy123-lab/3DCoCaNgua.git
```

Open the cloned folder with Unity Hub using Unity `6000.3.16f1`.

## Required Setup

1. Install Unity `6000.3.16f1` or a compatible Unity 6.3 LTS version.
2. Open the project from Unity Hub.
3. Wait for Unity Package Manager to restore packages.
4. Open `Assets/Scenes/AuthScene.unity`.
5. Select the `DatabaseManager` object.
6. Paste your MongoDB connection string into `Connection String`.

The MongoDB URI is intentionally not committed to GitHub. Each developer must paste their own URI locally.

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

The project uses Unity Relay, so router port forwarding should not be needed.

## Notes

- Do not commit MongoDB credentials.
- If login/register fails, check the `DatabaseManager` connection string first.
- If Relay fails, check Unity Services project linking and internet access.
- Build folders and local IDE files are ignored by Git.
