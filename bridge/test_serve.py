import http.client
import json
import tempfile
import threading
import unittest
from pathlib import Path
from http.server import ThreadingHTTPServer
from serve import make_handler


class BridgeTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.path = Path(self.tmp.name) / 'snapshot.json'
        self.server = ThreadingHTTPServer(('127.0.0.1', 0), make_handler(self.path))
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join()
        self.tmp.cleanup()

    def request(self, path='/api/snapshot', method='GET', headers=None):
        connection = http.client.HTTPConnection('127.0.0.1', self.server.server_port)
        connection.request(method, path, headers=headers or {})
        response = connection.getresponse()
        result = response.status, response.read()
        connection.close()
        return result

    def write(self, **extra):
        data = dict(schemaVersion=1, kind='debug.snapshot', modId='ONI-Rookie100', source='game', state='loaded', tasks=[], duplicants=[])
        data.update(extra)
        self.path.write_text(json.dumps(data), encoding='utf-8')

    def test_missing_snapshot_is_not_fake_data(self):
        code, body = self.request()
        self.assertEqual(code, 404)
        self.assertNotIn('tasks', json.loads(body))

    def test_valid_native_snapshot_preserves_unknown(self):
        self.write(duplicants=[dict(name='test fixture', health=None)], counts=None)
        code, body = self.request()
        self.assertEqual(code, 200)
        self.assertIsNone(json.loads(body)['duplicants'][0]['health'])

    def test_disconnected_snapshot_passes_through(self):
        self.write(state='disconnected')
        self.assertEqual(json.loads(self.request()[1])['state'], 'disconnected')

    def test_non_game_or_broken_snapshot_rejected(self):
        self.write(source='simulation')
        self.assertEqual(self.request()[0], 422)
        self.path.write_text('{', encoding='utf-8')
        self.assertEqual(self.request()[0], 422)

    def test_unexpected_host_and_origin_rejected(self):
        self.assertEqual(self.request(headers={'Host': 'other.example'})[0], 403)
        self.assertEqual(self.request(headers={'Origin': 'https://other.example'})[0], 403)

    def test_paths_and_mutations_not_supported(self):
        self.assertEqual(self.request('/../Rookie100.csproj')[0], 404)
        self.assertEqual(self.request(method='POST')[0], 501)

    def test_viewer_and_scripts_are_served(self):
        for path in ['/', '/app.js', '/style.css']:
            self.assertEqual(self.request(path)[0], 200)


if __name__ == '__main__':
    unittest.main()
