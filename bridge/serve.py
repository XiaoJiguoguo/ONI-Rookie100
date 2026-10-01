"""Read-only loopback viewer for the game's debug.snapshot v1 export."""
import argparse
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit


def load_snapshot(path):
    with path.open('rb') as stream:
        raw = stream.read(4 * 1024 * 1024 + 1)
    if len(raw) > 4 * 1024 * 1024:
        raise ValueError('snapshot too large')
    data = json.loads(raw.decode('utf-8-sig'))
    if not isinstance(data, dict) or data.get('schemaVersion') != 1 or data.get('kind') != 'debug.snapshot' or data.get('modId') != 'ONI-Rookie100' or data.get('source') != 'game':
        raise ValueError('unsupported snapshot')
    return data


def make_handler(snapshot):
    root = Path(__file__).resolve().parent

    class Handler(BaseHTTPRequestHandler):
        def respond(self, status, content, mime):
            self.send_response(status)
            self.send_header('Content-Type', mime)
            self.send_header('Content-Length', str(len(content)))
            self.send_header('Cache-Control', 'no-store')
            self.send_header('X-Content-Type-Options', 'nosniff')
            self.send_header('Content-Security-Policy', "default-src 'self'; style-src 'self'; script-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'")
            self.end_headers()
            self.wfile.write(content)

        def error_json(self, status, message):
            self.respond(status, json.dumps({'error': message}, ensure_ascii=False).encode('utf-8'), 'application/json; charset=utf-8')

        def do_GET(self):
            port = self.server.server_port
            allowed = {f'127.0.0.1:{port}', f'localhost:{port}'}
            if self.headers.get('Host') not in allowed:
                return self.error_json(403, '仅接受本机地址')
            origin = self.headers.get('Origin')
            if origin and origin not in {f'http://{host}' for host in allowed}:
                return self.error_json(403, '仅接受同源请求')
            path = urlsplit(self.path).path
            if path == '/api/snapshot':
                try:
                    data = load_snapshot(snapshot)
                    return self.respond(200, json.dumps(data, ensure_ascii=False).encode('utf-8'), 'application/json; charset=utf-8')
                except FileNotFoundError:
                    return self.error_json(404, '尚未生成快照，请在游戏中启用本地模组并载入存档')
                except (ValueError, UnicodeError):
                    return self.error_json(422, '快照格式无效，请确认使用本次构建的模组')
                except OSError:
                    return self.error_json(503, '快照暂不可读，稍后重试')
            files = {'/': ('index.html', 'text/html; charset=utf-8'), '/app.js': ('app.js', 'text/javascript; charset=utf-8'), '/style.css': ('style.css', 'text/css; charset=utf-8')}
            if path not in files:
                return self.error_json(404, '不存在此路径')
            name, mime = files[path]
            self.respond(200, (root / name).read_bytes(), mime)

        def log_message(self, format, *args):
            pass

    return Handler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--snapshot', type=Path, required=True)
    parser.add_argument('--port', type=int, default=4173)
    args = parser.parse_args()
    server = ThreadingHTTPServer(('127.0.0.1', args.port), make_handler(args.snapshot.resolve()))
    print(f'Rookie100 viewer: http://127.0.0.1:{server.server_port}', flush=True)
    print(f'Snapshot: {args.snapshot.resolve()}', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == '__main__':
    main()
