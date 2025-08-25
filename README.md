# WindowRPC
KDE Plasma Wayland port of WindowRPC. Proper overrides has not been implemented(yet).

This version also uses a system tray icon instead of keyboard shortcuts.

PRs for new icons and overrides should be done in the main branch if the app is crossplatform and is popular(e.g Discord). Otherwise, if it is a niche app, or not used on the Windows side often(or doesn't exist on Windows at all, e.g KWrite), the PR should be done in these conditions:

If the app is meant for a single desktop enviroment(e.g, Dolphin in KDE Plasma), PR should be done in the kde-linux branch, same goes for other desktop enviroments(gnome-linux, etc.). If the app is meant to be used on any desktop enviroments(e.g Bottles, EasyEffects, etc.), the **ICON** PR should be created in icons branch, and the overrides.json PR should be created in the respective desktop enviroment branch.
