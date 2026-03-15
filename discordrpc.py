#!/usr/bin/env python3
"""
Discord Rich Presence for Windows
- Uses pywin32 for window detection
- Uses WinRT (winrt-Windows.Media.Control) for accurate media detection
- Only triggers media overrides when playback is actually Playing/Paused
"""

import time
import threading
import subprocess
import json
import sys
import os
import pystray
import requests
from PIL import Image
from pypresence import Presence

# Windows-specific imports
import win32gui
import win32process

# ============================================================================
# WinRT Media Detection (Windows 10/11 only) - Properly Guarded
# ============================================================================
WINRT_AVAILABLE = False
try:
    from winrt.windows.media.control import (
        GlobalSystemMediaTransportControlsSessionManager as SessionsManager,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus as PlaybackStatus,
    )
    import asyncio
    import nest_asyncio
    nest_asyncio.apply()  # Allow nested event loops (required for pystray compatibility)
    WINRT_AVAILABLE = True
except ImportError:
    pass  # WinRT not available - media detection will gracefully skip

# Discord client ID
client_id = "1275126036262031452"
RPC = Presence(client_id)

# ============================================================================
# File Check + JSON Loading
# ============================================================================
def run_filecheck():
    """Run filecheck.py to ensure JSON files exist and are correctly set up."""
    try:
        flags = subprocess.CREATE_NO_WINDOW if sys.platform == 'win32' else 0
        subprocess.run([sys.executable, 'filecheck.py'], check=True, creationflags=flags, capture_output=True)
        print("Filecheck completed successfully.")
    except subprocess.CalledProcessError as e:
        print(f"Error running filecheck.py: {e}")
    except FileNotFoundError:
        print("filecheck.py not found - skipping")

run_filecheck()

def load_json(filename):
    try:
        with open(filename, 'r', encoding='utf-8') as file:
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

# ============================================================================
# Window Helpers (Windows native via pywin32)
# ============================================================================
def get_active_window_title():
    """Get the title of the currently active window on Windows."""
    try:
        hwnd = win32gui.GetForegroundWindow()
        if hwnd:
            title = win32gui.GetWindowText(hwnd)
            if title and title.strip():
                return title.strip()
    except Exception as e:
        print(f"Error getting window title: {e}")
    return "No active window"

def get_all_window_titles():
    """Get titles of all visible windows on Windows."""
    titles = []
    def enum_windows_callback(hwnd, results):
        if win32gui.IsWindowVisible(hwnd):
            title = win32gui.GetWindowText(hwnd)
            if title and title.strip():
                results.append(title.strip())
    try:
        win32gui.EnumWindows(enum_windows_callback, titles)
    except Exception as e:
        print(f"Error enumerating windows: {e}")
    return titles

# ============================================================================
# WinRT Media Detection (Windows 10/11 - Proper Implementation)
# ============================================================================
def _run_winrt_async(coro):
    """
    Run a WinRT async coroutine synchronously.
    Uses nest_asyncio to handle nested event loops safely.
    """
    return asyncio.run(coro)

def get_media_session():
    """
    Get current media session via WinRT.
    Returns session object or None if unavailable/no active session.
    """
    if not WINRT_AVAILABLE:
        return None
    try:
        async def _request():
            return await SessionsManager.request_async()
        
        sessions = _run_winrt_async(_request())
        if not sessions:
            return None
        session = sessions.get_current_session()
        # Only return if session has a real source app (filters ghost sessions)
        if session and session.source_app_user_model_id:
            return session
    except Exception as e:
        print(f"WinRT session error: {e}")
    return None

def get_media_info(player_hint=None):
    """
    Get media metadata ONLY if playback is actively Playing or Paused.
    Returns dict with standard keys + '_status' for override logic.
    
    Keys: mtitle, martist, malbum, mtotal, mcollapsed, mplayer, _status
    _status: 'playing', 'paused', or None
    """
    # Default "no media" response
    no_media = {
        "mtitle": "No media playing",
        "martist": "Unknown artist",
        "malbum": "Unknown album",
        "mtotal": "0:00",
        "mcollapsed": "0:00",
        "mplayer": "",
        "_status": None  # Internal: 'playing', 'paused', or None
    }
    
    if not WINRT_AVAILABLE:
        return no_media
    
    try:
        session = get_media_session()
        if not session:
            return no_media
        
        # Get playback status FIRST - if not playing/paused, skip metadata fetch
        playback_info = session.get_playback_info()
        status = playback_info.playback_status
        
        # Only return media info if actually playing or paused
        if status != PlaybackStatus.PLAYING:
            return no_media
        
        media = no_media.copy()
        media["_status"] = "playing" if status == PlaybackStatus.PLAYING else "paused"
        media["mplayer"] = session.source_app_user_model_id or "Unknown"
        
        # Fetch metadata properties asynchronously
        async def _get_props():
            return await session.try_get_media_properties_async()
        
        props = _run_winrt_async(_get_props())
        if props:
            if props.title: media["mtitle"] = props.title
            if props.artist: media["martist"] = props.artist
            if props.album_title: media["malbum"] = props.album_title
        
        # Get timeline for duration/position
        timeline = session.get_timeline_properties()
        if timeline:
            if timeline.end_time:
                total_sec = int(timeline.end_time.total_seconds())
                if total_sec > 0:
                    media["mtotal"] = f"{total_sec // 60}:{total_sec % 60:02d}"
            if timeline.position:
                elapsed = int(timeline.position.total_seconds())
                media["mcollapsed"] = f"{elapsed // 60}:{elapsed % 60:02d}"
            elif media["_status"] == "paused":
                media["mcollapsed"] = "Paused"
                
        return media
    except Exception as e:
        print(f"WinRT media info error: {e}")
        return no_media

# ============================================================================
# Last.fm Artwork Fetching
# ============================================================================
def fetch_lastfm_artwork(artist, album, api_key):
    """Fetch album cover URL from Last.fm"""
    if not artist or not album or not api_key:
        return None
    try:
        url = "http://ws.audioscrobbler.com/2.0/"
        params = {
            "method": "album.getinfo",
            "api_key": api_key,
            "artist": artist,
            "album": album,
            "format": "json"
        }
        response = requests.get(url, params=params, timeout=5)
        data = response.json()
        images = data.get("album", {}).get("image", [])
        for img in reversed(images):
            if img.get("#text"):
                return img["#text"]
        return None
    except Exception as e:
        print(f"Error fetching Last.fm artwork: {e}")
        return None

# ============================================================================
# Timestamps and Override Timers
# ============================================================================
script_start_time = time.time()
override_start_times = {}

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

# ============================================================================
# Override Resolution Logic
# ============================================================================
def check_exe_override(window_title):
    all_titles = get_all_window_titles()
    now = time.time()

    # --- 1️⃣ Game Override (Highest Priority) ---
    best_non_ignore_game = None
    best_ignore_game = None
    for app_name, message in overrides.items():
        if message.get("override_mode") == "game":
            found = any(
                (t.lower() == app_name.lower() if message.get("match_mode") == "exact" else app_name.lower() in t.lower())
                for t in all_titles
            )
            if found:
                if message.get("ignore", False):
                    if best_ignore_game is None or len(app_name) > len(best_ignore_game[0]):
                        best_ignore_game = (app_name, message)
                else:
                    if best_non_ignore_game is None or len(app_name) > len(best_non_ignore_game[0]):
                        best_non_ignore_game = (app_name, message)

    if best_non_ignore_game is not None:
        app_name, message = best_non_ignore_game
        if app_name not in override_start_times:
            override_start_times[app_name] = now
        elapsed_override = now - override_start_times[app_name]
        elapsed_str = f"{int(elapsed_override // 60)}m {int(elapsed_override % 60)}s"
        print(f"Game override active: {app_name}")
        state_message = format_message(message.get('state', ''), window_title, '', elapsed_str)
        details_message = format_message(message.get('details', ''), window_title, '', elapsed_str)
        logo = message.get('logo', 'rpc_icon')
        return state_message, details_message, logo, app_name, None, False
    elif best_ignore_game is not None:
        print(f"Ignoring RPC for game: {best_ignore_game[0]}")
        return None, None, None, None, None, True

    # --- 2️⃣ Media Override (WinRT-Strict: Only if Playback is Active) ---
    media_info = get_media_info()
    playback_status = media_info.get("_status")  # 'playing', 'paused', or None
    active_player = media_info.get("mplayer") if playback_status else None
    
    best_non_ignore_media = None
    best_ignore_media = None
    
    # ONLY process media overrides if WinRT confirms playback is active
    if playback_status in ("playing", "paused"):
        for app_name, message in overrides.items():
            if message.get("override_mode") == "media":
                # Optional player filtering (substring match, case-insensitive)
                if message.get("player"):
                    player_filter = message["player"].lower()
                    if player_filter not in active_player.lower():
                        continue
                
                if message.get("ignore", False):
                    if best_ignore_media is None or len(app_name) > len(best_ignore_media[0]):
                        best_ignore_media = (app_name, message)
                else:
                    if best_non_ignore_media is None or len(app_name) > len(best_non_ignore_media[0]):
                        best_non_ignore_media = (app_name, message)

        if best_non_ignore_media is not None:
            app_name, message = best_non_ignore_media
            if app_name not in override_start_times:
                override_start_times[app_name] = now
            elapsed_override = now - override_start_times[app_name]
            elapsed_str = f"{int(elapsed_override // 60)}m {int(elapsed_override % 60)}s"
            print(f"Media override active: {app_name} (player: {active_player}, status: {playback_status})")
            
            state_message = format_message(message.get('state', ''), window_title, '', elapsed_str, active_player)
            details_message = format_message(message.get('details', ''), window_title, '', elapsed_str, active_player)
            
            # Clean logo URL (trim whitespace that breaks image loading)
            logo = message.get('logo', 'rpc_icon')
            if isinstance(logo, str):
                logo = logo.strip()
                
            # Fetch Last.fm artwork if configured
            if message.get("artwork"):
                artwork_url = fetch_lastfm_artwork(
                    media_info["martist"],
                    media_info["malbum"],
                    message["artwork"].strip()
                )
                if artwork_url:
                    logo = artwork_url
                    
            return state_message, details_message, logo, app_name, active_player, False
            
        elif best_ignore_media is not None:
            print(f"Ignoring RPC for media: {best_ignore_media[0]}")
            return None, None, None, None, None, True

    # --- 3️⃣ Normal Overrides (Window Title Matching) ---
    best_non_ignore_normal = None
    best_ignore_normal = None
    for app_name, message in sorted_overrides:
        match_mode = message.get("match_mode", "inline")
        matched = (window_title.lower() == app_name.lower() if match_mode == "exact" else app_name.lower() in window_title.lower())
        if matched:
            if message.get("ignore", False):
                if best_ignore_normal is None or len(app_name) > len(best_ignore_normal[0]):
                    best_ignore_normal = (app_name, message)
            else:
                if best_non_ignore_normal is None or len(app_name) > len(best_non_ignore_normal[0]):
                    best_non_ignore_normal = (app_name, message)

    if best_non_ignore_normal is not None:
        app_name, message = best_non_ignore_normal
        if app_name not in override_start_times:
            override_start_times[app_name] = now
        elapsed_total = now - script_start_time
        elapsed_override = now - override_start_times[app_name]
        total_elapsed_str = f"{int(elapsed_total // 60)}m {int(elapsed_total % 60)}s"
        override_elapsed_str = f"{int(elapsed_override // 60)}m {int(elapsed_override % 60)}s"
        print(f"Override found for {window_title}: {message}")
        state_message = format_message(message.get('state', ''), window_title, total_elapsed_str, override_elapsed_str)
        details_message = format_message(message.get('details', ''), window_title, total_elapsed_str, override_elapsed_str)
        logo = message.get('logo', 'rpc_icon')
        return state_message, details_message, logo, app_name, None, False
    elif best_ignore_normal is not None:
        print(f"Ignoring RPC for app: {best_ignore_normal[0]}")
        return None, None, None, None, None, True

    # Fallback: no overrides matched
    return None, None, 'rpc_icon', None, None, False

# ============================================================================
# RPC Update Loop
# ============================================================================
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
            state, details, logo, override_key, active_player, ignore = check_exe_override(active_window_title)

            if ignore:
                print("Hiding RPC due to ignore flag")
                try:
                    RPC.clear()
                except Exception as e:
                    print(f"Error clearing RPC: {e}")
                time.sleep(interval)
                continue

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
                    large_text="0.6.1-windows"
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

# ============================================================================
# System Tray Icon Logic (pystray)
# ============================================================================
def create_image():
    image_path = 'discord_icon.png'
    if not os.path.isabs(image_path):
        image_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), image_path)
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
            flags = subprocess.CREATE_NO_WINDOW if sys.platform == 'win32' else 0
            subprocess.run([sys.executable, 'guieditor.py'], check=True, creationflags=flags)
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

# ============================================================================
# Main Entry Point
# ============================================================================
if __name__ == "__main__":
    start_rpc_updates_thread()
    start_tray_icon()
