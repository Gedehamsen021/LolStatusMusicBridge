# Lol Status Music Bridge

Lol Status Music Bridge copies the song currently playing on Windows into your League of Legends status by using the local League Client API (LCU).

It is made for Windows players who want their LoL profile status to show the current Spotify track, while ignoring browser videos and other media sessions by default.

## Features

- Reads the current playing media session from Windows
- Updates your League of Legends status through the local LCU bridge
- Restores your previous LoL status when music stops
- Restores your previous LoL status on normal app shutdown
- Uses a simple `config.json` file for user settings

## Download And Run

For end users:

1. Download the latest release `.zip` from GitHub Releases.
2. Extract the zip anywhere on your PC.
3. Edit `config.json` next to the `.exe`.
4. Start `LolStatusMusicBridge.exe`.
5. Open League of Legends and play some music.

## Config

The release includes a `config.json` file next to the executable.

```json
{
  "statusPrefix": "Escutando",
  "pollSeconds": 5,
  "maxStatusLength": 124,
  "leagueLockfilePath": null,
  "allowedSourceApps": ["Spotify"]
}
```

Available settings:

- `statusPrefix`: Text shown before the song title, for example `Escutando` or `Now playing`
- `pollSeconds`: How often the app checks music and League status
- `maxStatusLength`: Maximum final status length sent to League
- `leagueLockfilePath`: Optional custom lockfile path if auto-detection fails
- `allowedSourceApps`: Only sessions whose source app id contains one of these values will be used. Default: `Spotify`

Environment variables still work and override the values from `config.json`.

## Build From Source

```powershell
dotnet build .\LolStatusMusicBridge\LolStatusMusicBridge.csproj
dotnet run --project .\LolStatusMusicBridge\LolStatusMusicBridge.csproj
```

## GitHub Actions

The repo includes a workflow at `.github/workflows/release.yml`.

- Running it manually builds a Windows release zip
- Pushing a tag like `v1.0.0` builds the same package automatically
- Tagged releases also create a GitHub Release and attach the generated zip

## Notes

- League of Legends must be open and logged in
- The app uses the local League Client API and is not affiliated with Riot Games
- The default config is Spotify-only, so browser videos like YouTube are ignored unless you change `allowedSourceApps`
- The app can restore the old status on normal shutdown, but no app can guarantee restore after a hard kill from Task Manager or an IDE force stop
- If your League installation is in a different folder, set `leagueLockfilePath`
