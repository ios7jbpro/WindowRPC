# WindowRPC
WindowRPC is a Python-based tool that automatically updates your Discord status with your currently focused window as Rich Presence. It offers flexible options such as custom statuses per application, allowing you to tailor your Discord presence to your preferences.

## Features
- Automatically updates Discord Rich Presence based on the currently active window.
- Customize your status for specific applications.
- Lightweight and easy to set up.
- Windows-only support (for now).

## Installation
To get started with WindowRPC:

### Clone the Repository
Clone this repository to your local machine:

```
git clone https://github.com/yourusername/WindowRPC.git
cd WindowRPC
```

### Install Dependencies
Ensure you have Python installed, then install the required dependencies using pip:

```
pip install time threading pygetwindow pypresence msvcrt json subprocess
```

### Create a Discord Application
Go to the [Discord Developer Portal](https://discord.com/developers/applications) and create a new application. Note the Application ID.

### Configure the Script
Open discordrpc.py in a text editor, and replace the placeholder Application ID with your own.

### Run the Script
Start the script using the command line:

```
python discordrpc.py
```

## Usage
Once the script is running, your Discord status will automatically update based on the active window. You can customize specific application statuses by modifying the script.

## Contribution
This project was intended to be AI-generated only. However, simple contributions that remain easy for AI to parse and understand are welcome. Please submit issues or pull requests that align with this guideline.

## Issues and Support
For any issues or questions, please open a ticket in the Issues tab.
