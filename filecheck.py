import os
import json

# Define the contents for the JSON files
default_content = {
    "default": {
        "details": "Currently using:",
        "state": "timestamp - appname",
        "interval": "5"
    }
}

overrides_content = {
    "Notepad": {
        "logo": "rpc_icon",
        "details": "Currently coding",
        "state": "appname"
    }
}

# File names
default_file = 'default.json'
overrides_file = 'overrides.json'

# Function to create a file with content if it does not exist
def create_file_if_not_exists(file_name, content):
    if not os.path.exists(file_name):
        with open(file_name, 'w') as file:
            json.dump(content, file, indent=4)
        print(f"{file_name} created with default content.")
    else:
        print(f"{file_name} already exists.")

# Create the files
create_file_if_not_exists(default_file, default_content)
create_file_if_not_exists(overrides_file, overrides_content)
