import tkinter as tk
from tkinter import ttk, messagebox
import json
import os
import copy

class JSONTreeViewer:
    def __init__(self, root, json_data):
        self.root = root
        self.json_data = json_data
        self.keys_order = list(json_data.keys())  # keep track of insertion order
        self.tree = ttk.Treeview(root)
        self.tree.pack(expand=True, fill='both')
        self.build_tree("", json_data)

    def build_tree(self, parent, data):
        if isinstance(data, dict):
            for key in self.keys_order:
                if key in data:
                    node = self.tree.insert(parent, 'end', text=str(key))
                    self.build_tree(node, data[key])
        elif isinstance(data, list):
            for i, item in enumerate(data):
                node = self.tree.insert(parent, 'end', text=f"[{i}]")
                self.build_tree(node, item)
        else:
            self.tree.insert(parent, 'end', text=str(data))

    def add_entry_to_json(self, entry, below_key=None):
        key = entry['name']
        # avoid overwriting existing key
        suffix = 1
        new_key = key
        while new_key in self.json_data:
            new_key = f"{key}_{suffix}"
            suffix += 1
        entry['name'] = new_key
        self.json_data[new_key] = {k:v for k,v in entry.items() if k != 'name'}
        if below_key and below_key in self.keys_order:
            idx = self.keys_order.index(below_key) + 1
            self.keys_order.insert(idx, new_key)
        else:
            self.keys_order.append(new_key)
        self.refresh_tree()
        return new_key  # return the final key for selection

    def edit_entry_in_json(self, key, new_entry):
        self.json_data[key] = {k:v for k,v in new_entry.items() if k != 'name'}
        self.refresh_tree()

    def move_entry(self, key, direction):
        if key not in self.keys_order:
            return
        idx = self.keys_order.index(key)
        if direction == "up" and idx > 0:
            self.keys_order[idx], self.keys_order[idx-1] = self.keys_order[idx-1], self.keys_order[idx]
        elif direction == "down" and idx < len(self.keys_order)-1:
            self.keys_order[idx], self.keys_order[idx+1] = self.keys_order[idx+1], self.keys_order[idx]
        self.refresh_tree()

    def delete_entry(self, key):
        if key in self.json_data:
            del self.json_data[key]
            if key in self.keys_order:
                self.keys_order.remove(key)
            self.refresh_tree()

    def refresh_tree(self):
        self.tree.delete(*self.tree.get_children())
        self.build_tree("", self.json_data)

    def save_json(self, filename="overrides.json"):
        try:
            with open(filename, "w", encoding="utf-8") as f:
                json.dump(self.json_data, f, indent=4)
            self.show_save_warning()
        except Exception as e:
            messagebox.showerror("Error", f"Failed to save JSON: {e}")

    def show_save_warning(self):
        warning_win = tk.Toplevel()
        warning_win.title("Saved")
        warning_win.geometry("400x150")
        tk.Label(
            warning_win,
            text="For the RPC to listen to new changes,\nright click the tray icon and click refresh files.",
            wraplength=380,
            justify="center"
        ).pack(expand=True, pady=20)
        tk.Button(warning_win, text="OK", command=warning_win.destroy).pack(pady=10)

def load_json_file(filename):
    if not os.path.exists(filename):
        messagebox.showerror("Error", f"{filename} not found.")
        return None
    try:
        with open(filename, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception as e:
        messagebox.showerror("Error", f"Failed to load JSON: {e}")
        return None

def show_aliases_window():
    aliases_win = tk.Toplevel()
    aliases_win.title("Aliases")
    aliases_win.geometry("500x400")

    tk.Label(aliases_win, text="Available Aliases:", font=("Arial", 12, "bold")).pack(anchor='w', padx=10, pady=5)

    aliases_text = (
        "appname → replaced with the detected full window title name. Example: 'Playing appname' → 'Playing XYZ'.\n\n"
        "timestamp → replaced with how long the app has been running for.\n\n"
        "totaltimestamp → replaced with how long the script has been running for. Same as timestamp but since script start.\n\n"
        "mtitle → replaced with the current media’s title (song or video).\n\n"
        "martist → replaced with the current media’s artist(s).\n\n"
        "malbum → replaced with the current media’s album.\n\n"
        "mtotal → replaced with the media’s total duration (mm:ss).\n\n"
        "mcollapsed → replaced with how long the media has been playing (mm:ss). If paused, shows 'Paused'."
    )

    text_widget = tk.Text(aliases_win, wrap="word")
    text_widget.pack(expand=True, fill="both", padx=10, pady=5)
    text_widget.insert("1.0", aliases_text)
    text_widget.config(state="disabled")  # make read-only

def open_add_entry_dialog(viewer, edit_key=None, below_key=None):
    dialog = tk.Toplevel()
    dialog.title("Edit Entry" if edit_key else "Add New Entry")
    dialog.geometry("400x520")  # increased height a little for new field

    entry_data = viewer.json_data.get(edit_key, {}) if edit_key else {}

    tk.Label(dialog, text="Name").pack(anchor='w', padx=10, pady=2)
    name_entry = tk.Entry(dialog, width=50)
    name_entry.pack(padx=10, pady=2)
    name_entry.insert(0, edit_key if edit_key else "")

    tk.Label(dialog, text="Logo URL").pack(anchor='w', padx=10, pady=2)
    logo_entry = tk.Entry(dialog, width=50)
    logo_entry.pack(padx=10, pady=2)
    logo_entry.insert(0, entry_data.get("logo", ""))

    tk.Label(dialog, text="Details").pack(anchor='w', padx=10, pady=2)
    details_entry = tk.Entry(dialog, width=50)
    details_entry.pack(padx=10, pady=2)
    details_entry.insert(0, entry_data.get("details", ""))

    tk.Label(dialog, text="State").pack(anchor='w', padx=10, pady=2)
    state_entry = tk.Entry(dialog, width=50)
    state_entry.pack(padx=10, pady=2)
    state_entry.insert(0, entry_data.get("state", ""))

    # override_mode section
    tk.Label(dialog, text="Override Mode").pack(anchor='w', padx=10, pady=2)
    override_var = tk.StringVar(value=entry_data.get("override_mode", "none"))
    modes = [("Media", "media"), ("Game", "game"), ("None", "none")]
    for text, mode in modes:
        tk.Radiobutton(dialog, text=text, variable=override_var, value=mode).pack(anchor='w', padx=20)

    # match_mode section
    tk.Label(dialog, text="Match Mode").pack(anchor='w', padx=10, pady=2)
    match_var = tk.StringVar(value=entry_data.get("match_mode", "unimportant"))
    match_modes = [("Unimportant", "unimportant"), ("Inline", "inline"), ("Exact", "exact")]
    for text, mode in match_modes:
        tk.Radiobutton(dialog, text=text, variable=match_var, value=mode).pack(anchor='w', padx=20)

    def submit():
        new_entry = {
            "name": name_entry.get(),
            "logo": logo_entry.get(),
            "details": details_entry.get(),
            "state": state_entry.get()
        }
        if override_var.get() != "none":
            new_entry["override_mode"] = override_var.get()
        if match_var.get() != "unimportant":
            new_entry["match_mode"] = match_var.get()
        elif "match_mode" in entry_data and match_var.get() == "unimportant":
            # remove field if previously set
            new_entry.pop("match_mode", None)

        if edit_key:
            viewer.edit_entry_in_json(edit_key, new_entry)
        else:
            viewer.add_entry_to_json(new_entry, below_key=below_key)
        dialog.destroy()

    tk.Button(dialog, text="Save", command=submit).pack(pady=10)


def main():
    root = tk.Tk()
    root.title("Override JSON Editor")
    root.geometry("1500x400")  # increased width by another 500

    json_data = load_json_file("overrides.json")
    if json_data is None:
        json_data = {}

    viewer = JSONTreeViewer(root, json_data)

    button_frame = tk.Frame(root)
    button_frame.pack(side='bottom', fill='x', padx=5, pady=5)

    # Buttons setup
    add_btn = tk.Button(button_frame, text="Add New Entry", command=lambda: open_add_entry_dialog(viewer))
    add_btn.pack(side='left', padx=5)

    add_below_btn = tk.Button(button_frame, text="Add Below Selected", state="disabled")
    add_below_btn.pack(side='left', padx=5)

    edit_btn = tk.Button(button_frame, text="Edit Selected", state="disabled")
    edit_btn.pack(side='left', padx=5)

    move_up_btn = tk.Button(button_frame, text="Move Up", state="disabled")
    move_up_btn.pack(side='left', padx=5)

    move_down_btn = tk.Button(button_frame, text="Move Down", state="disabled")
    move_down_btn.pack(side='left', padx=5)

    clone_btn = tk.Button(button_frame, text="Clone Selected", state="disabled")
    clone_btn.pack(side='left', padx=5)

    delete_btn = tk.Button(button_frame, text="Delete Selected", state="disabled")
    delete_btn.pack(side='left', padx=5)

    save_btn = tk.Button(button_frame, text="Apply / Save", command=lambda: viewer.save_json())
    save_btn.pack(side='left', padx=5)

    discard_btn = tk.Button(button_frame, text="Discard Changes", command=root.destroy)
    discard_btn.pack(side='left', padx=5)

    # Enable buttons on root key selection
    def on_tree_select(event):
        selected = viewer.tree.selection()
        if selected:
            parent = viewer.tree.parent(selected[0])
            if parent == "":
                edit_btn.config(state="normal")
                delete_btn.config(state="normal")
                move_up_btn.config(state="normal")
                move_down_btn.config(state="normal")
                add_below_btn.config(state="normal")
                clone_btn.config(state="normal")
            else:
                edit_btn.config(state="disabled")
                delete_btn.config(state="disabled")
                move_up_btn.config(state="disabled")
                move_down_btn.config(state="disabled")
                add_below_btn.config(state="disabled")
                clone_btn.config(state="disabled")
        else:
            edit_btn.config(state="disabled")
            delete_btn.config(state="disabled")
            move_up_btn.config(state="disabled")
            move_down_btn.config(state="disabled")
            add_below_btn.config(state="disabled")
            clone_btn.config(state="disabled")

    viewer.tree.bind("<<TreeviewSelect>>", on_tree_select)

    # Button commands
    edit_btn.config(command=lambda: open_add_entry_dialog(
        viewer, edit_key=viewer.tree.item(viewer.tree.selection()[0])['text']
    ))
    delete_btn.config(command=lambda: viewer.delete_entry(viewer.tree.item(viewer.tree.selection()[0])['text']))
    move_up_btn.config(command=lambda: viewer.move_entry(viewer.tree.item(viewer.tree.selection()[0])['text'], "up"))
    move_down_btn.config(command=lambda: viewer.move_entry(viewer.tree.item(viewer.tree.selection()[0])['text'], "down"))
    add_below_btn.config(command=lambda: open_add_entry_dialog(
        viewer, below_key=viewer.tree.item(viewer.tree.selection()[0])['text']
    ))

    # Clone selected
    def clone_selected():
        sel_key = viewer.tree.item(viewer.tree.selection()[0])['text']
        if sel_key not in viewer.json_data:
            return
        entry_copy = copy.deepcopy(viewer.json_data[sel_key])
        # add below selected
        new_key = viewer.add_entry_to_json({"name": sel_key, **entry_copy}, below_key=sel_key)
        # select new entry in tree
        for item in viewer.tree.get_children():
            if viewer.tree.item(item)['text'] == new_key:
                viewer.tree.selection_set(item)
                viewer.tree.see(item)
                break
        # open edit dialog for new entry
        open_add_entry_dialog(viewer, edit_key=new_key)

    clone_btn.config(command=clone_selected)
    aliases_btn = tk.Button(button_frame, text="Aliases", command=show_aliases_window)
    aliases_btn.pack(side='left', padx=5)


    root.mainloop()

if __name__ == "__main__":
    main()
