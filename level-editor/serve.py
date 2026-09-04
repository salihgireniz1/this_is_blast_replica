"""serve.py - the level editor with the game's Levels folder wired in.

Layer: level editor (outside Assets/; Unity never sees it).
Responsibility: serving index.html and answering three requests about the folder the
  game reads levels from - list it, read one file, write one file - so the page needs no
  folder picker, no permission prompt and no browser that supports either. The folder is
  fixed: <repo>/Assets/00_GAME/Levels, the same path LevelFileTests walks.
NOT its responsibility: anything about a level's contents. The page validates before it
  writes; this server writes what it is given, into that one folder, and nothing else.

Run from anywhere:  python level-editor/serve.py   (opens the browser at localhost:8765)
Without it, index.html still works by double-click, with the folder picker or a download.
"""

import sys
import webbrowser
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parent
LEVELS = ROOT.parent / "Assets" / "00_GAME" / "Levels"
PORT = 8765
API = "/api/levels"


def safe_level_name(request_path):
    """The file name a request addresses, or None when it is not one plain .json name.

    The trust boundary: the page is local, but a URL is still a URL, so a name that
    climbs out of the folder or names something other than a .json file is refused.
    """
    if not request_path.startswith(API + "/"):
        return None
    name = unquote(request_path[len(API) + 1:])
    if "/" in name or "\\" in name or name in (".", ".."):
        return None
    if not name.lower().endswith(".json") or len(name) <= len(".json"):
        return None
    return name


class Handler(SimpleHTTPRequestHandler):
    """Static files from level-editor/, plus the three level requests."""

    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ROOT), **kwargs)

    def do_GET(self):
        """GET /api/levels lists the folder; GET /api/levels/<name> returns one file."""
        if self.path == API:
            names = sorted(p.name for p in LEVELS.glob("*.json"))
            self._send(200, "application/json",
                       '{"folder": %s, "files": %s}' % (_json_string(str(LEVELS)), _json_array(names)))
            return
        name = safe_level_name(self.path)
        if name is not None:
            path = LEVELS / name
            if not path.is_file():
                self.send_error(404, "No such level")
                return
            self._send(200, "application/json", path.read_text(encoding="utf-8"))
            return
        super().do_GET()

    def do_PUT(self):
        """PUT /api/levels/<name> writes the body as the file, bytes as sent (the page sends LF)."""
        name = safe_level_name(self.path)
        if name is None:
            self.send_error(400, "Expected /api/levels/<name>.json")
            return
        length = int(self.headers.get("Content-Length", "0"))
        (LEVELS / name).write_bytes(self.rfile.read(length))
        self.send_response(204)
        self.end_headers()

    def _send(self, status, content_type, text):
        body = text.encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", content_type + "; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, fmt, *args):
        """Only the level requests are worth a line; static file noise (and the favicon 404) is not."""
        if API in (fmt % args):
            super().log_message(fmt, *args)


def _json_string(text):
    """A JSON string literal, without importing json for two values."""
    return '"' + text.replace("\\", "\\\\").replace('"', '\\"') + '"'


def _json_array(names):
    return "[" + ", ".join(_json_string(n) for n in names) + "]"


def main():
    if not LEVELS.is_dir():
        sys.exit(f"Levels folder not found: {LEVELS}")
    server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    url = f"http://localhost:{PORT}/"
    print(f"Level editor at {url}\nLevels folder: {LEVELS}\nCtrl+C to stop.")
    webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
