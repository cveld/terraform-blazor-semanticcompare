"""Serve one file exactly once to one allowed origin, then exit.

Usage: python serve_once.py <file> [origin] [port] [timeout_seconds]

- Binds to 127.0.0.1 only.
- Serves the file at /plan.json, and only when the request carries the
  allowed Origin header. Everything else is a 404.
- CORS allows exactly that one origin (never "*").
- Exits after the first successful GET, or after the timeout.
"""
import os
import sys
import threading
from http.server import BaseHTTPRequestHandler, HTTPServer

PLAN = sys.argv[1]
ORIGIN = sys.argv[2] if len(sys.argv) > 2 else "http://localhost:5206"
PORT = int(sys.argv[3]) if len(sys.argv) > 3 else 18765
TIMEOUT = int(sys.argv[4]) if len(sys.argv) > 4 else 120


class Handler(BaseHTTPRequestHandler):
    def _cors(self):
        self.send_header("Access-Control-Allow-Origin", ORIGIN)
        self.send_header("Vary", "Origin")

    def do_OPTIONS(self):
        self.send_response(204)
        self._cors()
        self.send_header("Access-Control-Allow-Methods", "GET")
        self.end_headers()

    def do_GET(self):
        if self.path != "/plan.json" or self.headers.get("Origin") != ORIGIN:
            self.send_response(404)
            self.end_headers()
            return
        with open(PLAN, "rb") as f:
            data = f.read()
        self.send_response(200)
        self._cors()
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)
        self.wfile.flush()
        print("served", flush=True)
        threading.Timer(0.2, lambda: os._exit(0)).start()

    def log_message(self, *args):
        pass


threading.Timer(TIMEOUT, lambda: os._exit(1)).start()
HTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
