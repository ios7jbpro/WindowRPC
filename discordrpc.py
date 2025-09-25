import time
import threading
import subprocess
import json
import pystray
from PIL import Image
from pypresence import Presence

# Discord client ID
client_id = "1275126036262031452"
RPC = Presence(client_id)

# --- filecheck + json loading ------------------------------------------------
def run_filecheck():
    """Run filecheck.py to ensure JSON files exist and are correctly set up."""
    try:
        subprocess.run(['python3', 'filecheck.py'], check=True)
        print("Filecheck completed successfully.")
    except subprocess.CalledProcessError as e:
        print(f"Error running filecheck.py: {e}")

run_filecheck()

def load_json(filename):
    try:
        with open(filename, 'r') as file:
            return json.load(file)
    except Exception as e:
        print(f"Failed to load {filename}: {e}")
        return {}

def refresh_files():
    global overrides, sorted_overrides, default_settings, interval
    overrides = load_json('overrides.json')
    sorted_overrides = sorted(overrides.items(), key=lambda item: len(item[0]), reverse=True)
    default_settings = load_json('default.json').get('default', {})
    interval = int(default_settings.get('interval', 15))
    print("Files refreshed")

overrides = load_json('overrides.json')
sorted_overrides = sorted(overrides.items(), key=lambda item: len(item[0]), reverse=True)
default_settings = load_json('default.json').get('default', {})
interval = int(default_settings.get('interval', 15))

# --- window / media helpers --------------------------------------------------
def get_active_window_title():
    try:
        window_id = subprocess.check_output(['kdotool', 'getactivewindow']).decode('utf-8').strip()
        window_title = subprocess.check_output(['kdotool', 'getwindowname', window_id]).decode('utf-8').strip()
        return window_title
    except (subprocess.CalledProcessError, IndexError):
        return "No active window"

def get_all_window_titles():
    try:
        window_ids = subprocess.check_output(['kdotool', 'search', '--name', '.*']).decode('utf-8').splitlines()
        titles = []
        for wid in window_ids:
            try:
                title = subprocess.check_output(['kdotool', 'getwindowname', wid]).decode('utf-8').strip()
                if title:
                    titles.append(title)
            except subprocess.CalledProcessError:
                continue
        return titles
    except subprocess.CalledProcessError:
        return []

def get_active_player():
    try:
        players = subprocess.check_output(['playerctl', '-l']).decode('utf-8').splitlines()
        for player in players:
            player = player.strip()
            if not player:
                continue
            try:
                status = subprocess.check_output(['playerctl', '-p', player, 'status']).decode('utf-8').strip()
            except subprocess.CalledProcessError:
                continue
            if status == "Playing":
                return player
        return None
    except subprocess.CalledProcessError:
        return None

def get_media_info(player=None):
    media = {
        "mtitle": "No media playing",
        "martist": "Unknown artist",
        "malbum": "Unknown album",
        "mtotal": "0:00",
        "mcollapsed": "0:00",
        "mplayer": ""
    }
    if not player:
        return media
    try:
        def get_meta(field):
            try:
                return subprocess.check_output(['playerctl', '-p', player, 'metadata', field]).decode('utf-8').strip()
            except subprocess.CalledProcessError:
                return ""

        title = get_meta('title')
        if title: media["mtitle"] = title
        artist = get_meta('artist')
        if artist: media["martist"] = artist
        album = get_meta('album')
        if album: media["malbum"] = album

        length_us = get_meta('mpris:length')
        if length_us.isdigit():
            length_sec = int(length_us) // 1000000
            media["mtotal"] = f"{length_sec // 60}:{length_sec % 60:02d}"

        try:
            status = subprocess.check_output(['playerctl', '-p', player, 'status']).decode('utf-8').strip()
        except subprocess.CalledProcessError:
            status = ""

        if status == "Playing":
            try:
                elapsed_str = subprocess.check_output(['playerctl', '-p', player, 'position']).decode('utf-8').strip()
                elapsed_sec = float(elapsed_str)
                media["mcollapsed"] = f"{int(elapsed_sec // 60)}:{int(elapsed_sec % 60):02d}"
            except Exception:
                media["mcollapsed"] = "0:00"
        elif status == "Paused":
            media["mcollapsed"] = "Paused"
        else:
            media["mcollapsed"] = "Stopped"

        media["mplayer"] = player
    except Exception:
        pass
    return media

# --- timestamps and override timers -----------------------------------------
script_start_time = time.time()
override_start_times = {}  # { override_key: timestamp }

def format_message(template, window_title, total_elapsed_str, override_elapsed_str, active_player=None):
    if not isinstance(template, str):
        template = str(template or "")
    media_info = get_media_info(active_player)
    return (
        template
        .replace("appname", window_title)
        .replace("totaltimestamp", total_elapsed_str)
        .replace("timestamp", override_elapsed_str)
        .replace("mtitle", media_info["mtitle"])
        .replace("martist", media_info["martist"])
        .replace("malbum", media_info["malbum"])
        .replace("mtotal", media_info["mtotal"])
        .replace("mcollapsed", media_info["mcollapsed"])
        .replace("mplayer", media_info["mplayer"])
    )

def _make_times_for_override(key, reset_if_missing=False):
    now = time.time()
    total_elapsed = now - script_start_time
    if key not in override_start_times or (reset_if_missing and override_start_times[key] == 0):
        override_start_times[key] = now
    override_elapsed = now - override_start_times[key]
    total_elapsed_str = f"{int(total_elapsed // 60)}m {int(total_elapsed % 60)}s"
    override_elapsed_str = f"{int(override_elapsed // 60)}m {int(override_elapsed % 60)}s"
    return total_elapsed_str, override_elapsed_str

# --- override resolution logic -----------------------------------------------
def check_exe_override(window_title):
    all_titles = get_all_window_titles()

    # 1) game overrides
    for app_name, message in overrides.items():
        if message.get("override_mode") == "game":
            match_mode = message.get("match_mode", "unimportant")
            found = False
            if match_mode == "exact":
                found = any(app_name == t for t in all_titles)
            else:
                found = any(app_name.lower() in t.lower() for t in all_titles)

            if found:
                # game is active
                total_elapsed_str, override_elapsed_str = _make_times_for_override(app_name)
                state_message = format_message(message.get('state', ''), window_title, total_elapsed_str, override_elapsed_str)
                details_message = format_message(message.get('details', ''), window_title, total_elapsed_str, override_elapsed_str)
                logo = message.get('logo', 'rpc_icon')
                print(f"Game override active: {app_name}")
                override_start_times[app_name] = override_start_times.get(app_name, time.time())
                return state_message, details_message, logo, app_name, None
            else:
                # game window gone → reset timer for next activation
                override_start_times[app_name] = 0

    # 2) media overrides
    active_player = get_active_player()
    if active_player:
        for app_name, message in overrides.items():
            if message.get("override_mode") == "media":
                desired_player = message.get("player")
                if desired_player and desired_player.lower() != active_player.lower():
                    continue
                total_elapsed_str, override_elapsed_str = _make_times_for_override(app_name)
                state_message = format_message(message.get('state', ''), window_title, total_elapsed_str, override_elapsed_str, active_player)
                details_message = format_message(message.get('details', ''), window_title, total_elapsed_str, override_elapsed_str, active_player)
                logo = message.get('logo', 'rpc_icon')
                print(f"Media override active: {app_name} (player: {active_player})")
                return state_message, details_message, logo, app_name, active_player

    # 3) normal app override
    for app_name, message in sorted_overrides:
        match_mode = message.get("match_mode", "unimportant")
        matched = False
        if match_mode == "exact":
            matched = (app_name == window_title)
        else:
            matched = (app_name.lower() in window_title.lower())
        if matched:
            total_elapsed_str, override_elapsed_str = _make_times_for_override(app_name)
            state_message = format_message(message.get('state', ''), window_title, total_elapsed_str, override_elapsed_str)
            details_message = format_message(message.get('details', ''), window_title, total_elapsed_str, override_elapsed_str)
            logo = message.get('logo', 'rpc_icon')
            print(f"Override found for {window_title}: {message}")
            return state_message, details_message, logo, app_name, None

    # fallback
    return None, None, 'rpc_icon', None, None

# --- RPC update loop --------------------------------------------------------
def truncate_text(text, max_length=60):
    if not isinstance(text, str):
        text = str(text or "")
    if len(text) > max_length:
        return text[:max_length - 3] + "..."
    return text

rpc_enabled = True

def update_rpc():
    global interval
    try:
        RPC.connect()
    except Exception as e:
        print(f"Error connecting to Discord on start: {e}")

    while True:
        if rpc_enabled:
            active_window_title = get_active_window_title()
            print(f"Detected window: {active_window_title}")

            state, details, logo, override_key, active_player = check_exe_override(active_window_title)

            if override_key:
                total_elapsed_str, override_elapsed_str = _make_times_for_override(override_key)
            else:
                now = time.time()
                total_elapsed = now - script_start_time
                total_elapsed_str = f"{int(total_elapsed // 60)}m {int(total_elapsed % 60)}s"
                override_elapsed_str = total_elapsed_str

            if state and details:
                state_message = format_message(state, active_window_title, total_elapsed_str, override_elapsed_str, active_player)
                details_message = format_message(details, active_window_title, total_elapsed_str, override_elapsed_str, active_player)
            else:
                state_message = format_message(default_settings.get('state', ''), active_window_title, total_elapsed_str, override_elapsed_str, None)
                details_message = format_message(default_settings.get('details', ''), active_window_title, total_elapsed_str, override_elapsed_str, None)
                logo = 'rpc_icon'

            state_message = truncate_text(state_message)
            details_message = truncate_text(details_message)

            print(f"Updating RPC with state: '{state_message}', details: '{details_message}', logo: '{logo}'")
            try:
                RPC.update(
                    state=state_message,
                    details=details_message,
                    large_image=logo,
                    large_text="0.6.1"
                )
            except Exception as e:
                print(f"Error updating RPC: {e}. Attempting reconnect...")
                try:
                    RPC.reconnect()
                except Exception:
                    pass
        else:
            try:
                RPC.close()
            except Exception:
                pass

        time.sleep(interval)

# --- System Tray Icon Logic --------------------------------------------------
def create_image():
    image_path = 'discord_icon.png'
    return Image.open(image_path)

def toggle_rpc(icon, item):
    global rpc_enabled
    rpc_enabled = not rpc_enabled
    status = "Enabled" if rpc_enabled else "Disabled"
    print(f"RPC is now {status}")
    if rpc_enabled:
        threading.Thread(target=update_rpc, daemon=True).start()

def launch_gui_editor(icon, item):
    def run_editor():
        try:
            subprocess.run(['python3', 'guieditor.py'], check=True)
        except subprocess.CalledProcessError as e:
            print(f"Error launching GUI Editor: {e}")
    threading.Thread(target=run_editor, daemon=True).start()
    print("GUI Editor launched in background")

def refresh_settings(icon, item):
    refresh_files()

def start_rpc_updates_thread():
    rpc_thread = threading.Thread(target=update_rpc, daemon=True)
    rpc_thread.start()

def on_exit(icon, item):
    icon.stop()

def start_tray_icon():
    image = create_image()
    icon = pystray.Icon(
        'discordrpc_icon',
        image,
        'Discord RPC',
        menu=pystray.Menu(
            pystray.MenuItem('Toggle RPC', toggle_rpc),
            pystray.MenuItem('Refresh Files', refresh_settings),
            pystray.MenuItem('Launch GUI Editor', launch_gui_editor),
            pystray.MenuItem('Exit', on_exit)
        )
    )
    icon.run()

# --- main -------------------------------------------------------------------
if __name__ == "__main__":
    start_rpc_updates_thread()
    start_tray_icon()
