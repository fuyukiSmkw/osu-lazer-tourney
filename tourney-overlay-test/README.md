# Tournament Overlay Test

Minimal test overlay for the osu!lazer tournament plugin's WebSocket API.

## Usage

1. Start osu!lazer with the tournament plugin loaded
2. Enter a multiplayer room and start spectating a match
3. Serve this folder with any static file server:

```bash
# Using Python
python3 -m http.server 8080

# Using Node.js
npx serve .

# Using PHP
php -S localhost:8080
```

4. Open `http://localhost:8080` in your browser (or OBS Browser source)

## WebSocket Endpoint

Connects to `ws://127.0.0.1:50727/ws` (the tournament plugin's WebSocket server)

## Data Format

The overlay expects the same JSON structure as tosu's `/ws` v1 endpoint:

```json
{
  "tourney": {
    "manager": {
      "ipcState": 0,
      "bools": { "scoreVisible": true },
      "gameplay": { "score": { "left": 12345, "right": 67890 } },
      "chat": []
    },
      "ipcClients": [
      {
        "team": "left",
        "user": { "name": "Player1", "username": "Player1" },
        "spectating": { "name": "Player1", "country": "US", "userID": 12345 },
        "gameplay": {
          "score": 1234567,
          "accuracy": 0.985,
          "combo": { "current": 150, "max": 200 },
          "hp": { "normal": 0.85 },
          "hits": { "300": 500, "geki": 20, "100": 10, "katu": 5, "50": 2, "0": 0, "unstableRate": 45.2, "hitErrorArray": [...] },
          "pp": { "current": 123.4, "fc": 200.1, "maxThisPlay": 180.5 },
          "mods": { "num": 0, "str": "HDHR" }
        }
      }
    ]
  },
  "menu": {
    "state": 11,
    "gameMode": 0,
    "mods": { "num": 0, "str": "HD,HR" },
    "bm": {
      "id": 123456,
      "metadata": { "title": "Song Title", "artist": "Artist", "creator": "Mapper", "difficulty": "Hard" },
      "md5": "abcdef...",
      "set": 789012,
      "rankedStatus": 4,
      "time": { "full": 180000, "current": 45000 },
      "stats": { "CS": 4.2, "AR": 9.0, "OD": 8.0, "HP": 5.5, "SR": 5.8, "BPM": { "common": 180 } },
      "originalStats": { "CS": 4.0, "AR": 9.0, "OD": 8.0, "HP": 5.0, "SR": 5.5 }
    }
  }
}
```

Times are milliseconds (matching tosu). `stats` holds mod-adjusted values;
`originalStats` holds the stored unmodded values. The server also exposes
`GET /backgroundImage` (same port) serving the current map's background.

## Notes

- The overlay auto-reconnects on disconnect (3s delay)
- All data updates in real-time as the tournament plugin broadcasts (10Hz)
- Works in OBS Studio Browser source - just point to the local URL
- No external dependencies - pure vanilla HTML/JS