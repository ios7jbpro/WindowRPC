# customization guide

everything is controlled through:

```
overrides.json
```

each key in the JSON is something to match against a window title.

basic structure:

```json
{
  "App Name": {
    "override_mode": "normal",
    "match_mode": "inline",
    "state": "Using appname",
    "details": "Active for timestamp",
    "logo": "rpc_icon"
  }
}
```

---

## override modes

### 1️⃣ normal

matches the **active window title**

```json
"Firefox": {
  "override_mode": "normal",
  "match_mode": "inline",
  "state": "Browsing",
  "details": "On appname"
}
```

* `inline` → matches if text exists anywhere
* `exact` → must match exactly

---

### 2️⃣ game (highest priority)

triggers if **any matching window exists**, even if not focused.

```json
"Minecraft": {
  "override_mode": "game",
  "match_mode": "inline",
  "state": "Playing Minecraft",
  "details": "Session: timestamp"
}
```

game overrides override everything else.

---

### 3️⃣ media

triggers when a media player is actively playing.

```json
"Spotify": {
  "override_mode": "media",
  "player": "spotify",
  "state": "Listening to mtitle",
  "details": "By martist"
}
```

available media placeholders:

* `mtitle`
* `martist`
* `malbum`
* `mtotal`
* `mcollapsed`
* `mplayer`

---

## hiding RPC for specific apps

add:

```json
"ignore": true
```

example:

```json
"Steam": {
  "override_mode": "normal",
  "ignore": true
}
```

this completely clears discord presence while steam is active.

---

## available placeholders

you can use these in `state` or `details`:

| placeholder      | meaning                       |
| ---------------- | ----------------------------- |
| `appname`        | window title                  |
| `timestamp`      | time since override activated |
| `totaltimestamp` | time since script started     |
| `mtitle`         | song title                    |
| `martist`        | artist                        |
| `malbum`         | album                         |
| `mtotal`         | total duration                |
| `mcollapsed`     | elapsed or paused             |
| `mplayer`        | media player name             |

example:

```json
"Code": {
  "override_mode": "normal",
  "state": "Coding in appname",
  "details": "Focused for timestamp"
}
```

---

## default fallback

if nothing matches, `default.json` is used:

```json
{
  "default": {
    "state": "Using appname",
    "details": "Active for totaltimestamp",
    "interval": 15
  }
}
```

---

## applying changes

you don’t need to restart the script.

use tray icon → **Refresh Files**
