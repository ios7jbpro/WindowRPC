import time
import threading
import pygetwindow as gw
from pypresence import Presence
import msvcrt  # For listening to keypresses on Windows
import json
import subprocess  # For running the filecheck script

# Discord client ID
client_id = "1275126036262031452"
RPC = Presence(client_id)
RPC.connect()

def run_filecheck():
    """Run filecheck.py to ensure JSON files exist and are correctly set up."""
    try:
        subprocess.run(['python', 'filecheck.py'], check=True)
        print("Filecheck completed successfully.")
    except subprocess.CalledProcessError as e:
        print(f"Error running filecheck.py: {e}")

# Run filecheck before proceeding
run_filecheck()

# Load overrides and default settings from JSON files
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
interval = int(default_settings.get('interval', 15))  # Default to 15 seconds if not specified

def get_active_window_title():
    window = gw.getActiveWindow()
    return window.title if window else "No active window"

def format_message(template, window_title, elapsed_str):
    """ Replace placeholders in the template with actual values """
    return template.replace("appname", window_title).replace("timestamp", elapsed_str)

def check_exe_override(window_title):
    for app_name, message in sorted_overrides:
        if app_name.lower() in window_title.lower():
            print(f"Override found for {window_title}: {message}")
            elapsed_time = time.time() - start_time
            elapsed_str = f"{int(elapsed_time // 60)}m {int(elapsed_time % 60)}s"
            state_message = format_message(message['state'], window_title, elapsed_str)
            details_message = format_message(message['details'], window_title, elapsed_str)
            logo = message.get('logo', 'rpc_icon')  # Get the logo if present, otherwise default to 'rpc_icon'
            return state_message, details_message, logo
    return None, None, 'rpc_icon'  # Default to 'rpc_icon' if no override is found

def toggle_rpc():
    global rpc_enabled
    while True:
        if msvcrt.kbhit():
            key = msvcrt.getch()
            if key == b't':  # Press 't' to toggle RPC
                rpc_enabled = not rpc_enabled
                status = "Enabled" if rpc_enabled else "Disabled"
                print(f"RPC is now {status}")
            elif key == b'r':  # Press 'r' to refresh the files
                refresh_files()

rpc_enabled = True
start_time = time.time()

def update_rpc():
    global interval
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
                logo = 'rpc_icon'  # Default logo if no override

            print(f"Updating RPC with state: '{state_message}', details: '{details_message}', and logo: '{logo}'")
            RPC.update(
                state=state_message,
                details=details_message,
                large_image=logo,  # Use the dynamic logo
                large_text="0.6.1"
            )
        else:
            RPC.update(
                state=f"{elapsed_str} - Current window cannot be detected!",
                details="Currently using:"
            )
        
        time.sleep(interval)

# Start the toggle thread
toggle_thread = threading.Thread(target=toggle_rpc, daemon=True)
toggle_thread.start()

# Run the RPC update loop
update_rpc()
