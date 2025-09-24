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
# RPC.connect() is moved to be called within the main loop to handle reconnections

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
    global overrides, sorted_overrides, default_settings
    overrides = load_json('overrides.json')
    sorted_overrides = sorted(overrides.items(), key=lambda item: len(item[0]), reverse=True)
    default_settings = load_json('default.json').get('default', {})
    print("Files refreshed")

overrides = load_json('overrides.json')
sorted_overrides = sorted(overrides.items(), key=lambda item: len(item[0]), reverse=True)
default_settings = load_json('default.json').get('default', {})
interval = int(default_settings.get('interval', 15))

def get_active_window_title():
    try:
        window_id = subprocess.check_output(['kdotool', 'getactivewindow']).decode('utf-8').strip()
        window_title = subprocess.check_output(['kdotool', 'getwindowname', window_id]).decode('utf-8').strip()
        return window_title
    except (subprocess.CalledProcessError, IndexError):
        return "No active window"

def get_active_player():
    try:
        players = subprocess.check_output(['playerctl', '-l']).decode('utf-8').splitlines()
        for player in players:
            status = subprocess.check_output(['playerctl', '-p', player, 'status']).decode('utf-8').strip()
            if status == "Playing":
                return player
        # fallback: none playing
        return None
    except subprocess.CalledProcessError:
        return None


def get_media_info(player=None):
    """Fetch metadata for a specific player (or default if None)."""
    media = {
        "mtitle": "No media playing",
        "martist": "Unknown artist",
        "malbum": "Unknown album",
        "mtotal": "0:00",
        "mcollapsed": "0:00"
    }
    if not player:
        return media
    try:
        def get_meta(field):
            return subprocess.check_output(['playerctl', '-p', player, 'metadata', field]).decode('utf-8').strip()

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

        status = subprocess.check_output(['playerctl', '-p', player, 'status']).decode('utf-8').strip()
        if status == "Playing":
            elapsed_str = subprocess.check_output(['playerctl', '-p', player, 'position']).decode('utf-8').strip()
            elapsed_sec = float(elapsed_str)
            media["mcollapsed"] = f"{int(elapsed_sec // 60)}:{int(elapsed_sec % 60):02d}"
        elif status == "Paused":
            media["mcollapsed"] = "Paused"
        else:
            media["mcollapsed"] = "Stopped"

    except subprocess.CalledProcessError:
        pass

    return media




def get_all_window_titles():
    try:
        window_ids = subprocess.check_output(['kdotool', 'search', '--name', '.*']).decode('utf-8').splitlines()
        titles = []
        for wid in window_ids:
            try:
                title = subprocess.check_output(['kdotool', 'getwindowname', wid]).decode('utf-8').strip()
                titles.append(title)
            except subprocess.CalledProcessError:
                continue
        return titles
    except subprocess.CalledProcessError:
        return []




def format_message(template, window_title, elapsed_str, active_player=None):
    """ Replace placeholders in the template with actual values """
    media_info = get_media_info(active_player)
    return (
        template
        .replace("appname", window_title)
        .replace("timestamp", elapsed_str)
        .replace("mtitle", media_info["mtitle"])
        .replace("martist", media_info["martist"])
        .replace("malbum", media_info["malbum"])
        .replace("mtotal", media_info["mtotal"])
        .replace("mcollapsed", media_info["mcollapsed"])
    )



def check_exe_override(window_title):
    all_titles = get_all_window_titles()

    # 1️⃣ game override (highest priority)
    for app_name, message in overrides.items():
        if message.get("override_mode") == "game":
            # check if any window contains the override name
            if any(app_name.lower() in t.lower() for t in all_titles):
                print(f"Game override active: {app_name}")
                elapsed_time = time.time() - start_time
                elapsed_str = f"{int(elapsed_time // 60)}m {int(elapsed_time % 60)}s"
                state_message = format_message(message.get('state', ''), window_title, elapsed_str)
                details_message = format_message(message.get('details', ''), window_title, elapsed_str)
                logo = message.get('logo', 'rpc_icon')
                return state_message, details_message, logo

    # 2️⃣ media override (same logic as before)
    active_player = get_active_player()
    if active_player:
        for app_name, message in overrides.items():
            if message.get("override_mode") == "media":
                if message.get("player") and message["player"].lower() != active_player.lower():
                    continue
                print(f"Media override active: {app_name} (player: {active_player})")
                elapsed_time = time.time() - start_time
                elapsed_str = f"{int(elapsed_time // 60)}m {int(elapsed_time % 60)}s"
                state_message = format_message(message.get('state', ''), window_title, elapsed_str, active_player)
                details_message = format_message(message.get('details', ''), window_title, elapsed_str, active_player)
                logo = message.get('logo', 'rpc_icon')
                return state_message, details_message, logo

    # 3️⃣ normal app override
    for app_name, message in sorted_overrides:
        if app_name.lower() in window_title.lower():
            print(f"Override found for {window_title}: {message}")
            elapsed_time = time.time() - start_time
            elapsed_str = f"{int(elapsed_time // 60)}m {int(elapsed_time % 60)}s"
            state_message = format_message(message['state'], window_title, elapsed_str)
            details_message = format_message(message['details'], window_title, elapsed_str)
            logo = message.get('logo', 'rpc_icon')
            return state_message, details_message, logo

    # fallback
    return None, None, 'rpc_icon'



def truncate_text(text, max_length=60):
    """Ensure the text is no longer than max_length characters, adding '...' if truncated."""
    if len(text) > max_length:
        return text[:max_length - 3] + "..."
    return text

rpc_enabled = True
start_time = time.time()

def update_rpc():
    global interval
    try:
        RPC.connect()
    except Exception as e:
        print(f"Error connecting to Discord: {e}")
        time.sleep(10)
        return

    while True:
        elapsed_time = time.time() - start_time
        elapsed_str = f"{int(elapsed_time // 60)}m {int(elapsed_time % 60)}s"

        if rpc_enabled:
            active_window_title = get_active_window_title()
            print(f"Detected window: {active_window_title}")

            state, details, logo = check_exe_override(active_window_title)

            if state and details:
                state_message = format_message(state, active_window_title, elapsed_str)
                details_message = format_message(details, active_window_title, elapsed_str)
            else:
                state_message = format_message(default_settings.get('state', ''), active_window_title, elapsed_str)
                details_message = format_message(default_settings.get('details', ''), active_window_title, elapsed_str)
                logo = 'rpc_icon'

            state_message = truncate_text(state_message)
            details_message = truncate_text(details_message)

            print(f"Updating RPC with state: '{state_message}', details: '{details_message}', and logo: '{logo}'")
            try:
                RPC.update(
                    state=state_message,
                    details=details_message,
                    large_image=logo,
                    large_text="0.6.1"
                )
            except Exception as e:
                print(f"Error updating RPC: {e}. Retrying connection...")
                try:
                    RPC.reconnect()
                except:
                    pass
        else:
            try:
                RPC.close()
            except:
                pass

        time.sleep(interval)

# --- System Tray Icon Logic ---

def create_image():
    # Load the image from a file
    image_path = 'discord_icon.png'
    return Image.open(image_path)

def toggle_rpc(icon, item):
    global rpc_enabled
    rpc_enabled = not rpc_enabled
    status = "Enabled" if rpc_enabled else "Disabled"
    print(f"RPC is now {status}")
    if rpc_enabled:
        threading.Thread(target=update_rpc, daemon=True).start()
    else:
        pass

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
            pystray.MenuItem('Exit', on_exit)
        )
    )
    icon.run()

if __name__ == "__main__":
    start_rpc_updates_thread()
    start_tray_icon()
