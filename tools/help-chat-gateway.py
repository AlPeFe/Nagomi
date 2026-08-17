#!/usr/bin/env python3
"""Nagomi help-chat gateway (dev mock).

A tiny OpenAI-compatible chat-completions endpoint used to exercise the in-app
help chat without a real LLM provider. Serves POST /v1/chat/completions and
echoes a canned reply built from the last user message.

Usage:
    python tools/help-chat-gateway.py [--port 9100]

Then point Nagomi at it with, e.g.:
    HELPCHAT_ENABLED=true
    HELPCHAT_BASE_URL=http://host.docker.internal:9100/v1
    HELPCHAT_API_KEY=dev-gateway-key
    HELPCHAT_MODEL=nagomi-gateway
"""
import argparse
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.0"

    def _send(self, status: int, payload: dict) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_POST(self):  # noqa: N802
        if not self.path.startswith("/v1/chat/completions"):
            self._send(404, {"error": {"message": "not found"}})
            return

        length = int(self.headers.get("Content-Length", 0))
        raw = self.rfile.read(length) if length else b"{}"
        print(f"[gateway] body recibido: {raw!r}", flush=True)
        try:
            req = json.loads(raw.decode("utf-8"))
        except json.JSONDecodeError:
            req = {}

        messages = req.get("messages", [])
        user_last = next(
            (m.get("content", "") for m in reversed(messages) if m.get("role") == "user"),
            "",
        )
        reply = (
            f"[gateway] Has preguntado: «{user_last}». "
            "Este es un mock de desarrollo; conecta un proveedor real en .env para respuestas reales."
        )
        self._send(200, {
            "id": "chatcmpl-mock",
            "object": "chat.completion",
            "choices": [{"index": 0, "message": {"role": "assistant", "content": reply}, "finish_reason": "stop"}],
            "usage": {"prompt_tokens": 0, "completion_tokens": 0, "total_tokens": 0},
        })

    def do_GET(self):  # noqa: N802
        self._send(200, {"status": "ok"})


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=9100)
    args = parser.parse_args()
    server = ThreadingHTTPServer(("0.0.0.0", args.port), Handler)
    print(f"help-chat gateway escuchando en http://0.0.0.0:{args.port}/v1/chat/completions", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
