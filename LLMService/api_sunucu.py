"""Unity Editor/Windows için yalnız localhost üzerinde oyun API'si."""
import argparse
import json
import threading
import uuid
import urllib.error
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from robot import Game, Ollama, CONFIG, ROOT

FALLBACK_REPLY = 'Ne dediğini anlayamadım. Biraz daha açık anlatır mısın?'

def failure_code(exc):
    if isinstance(exc, urllib.error.HTTPError) and exc.code == 404:
        return 'model_missing'
    if isinstance(exc, TimeoutError) or isinstance(getattr(exc, 'reason', None), TimeoutError):
        return 'timeout'
    if isinstance(exc, urllib.error.URLError):
        return 'ollama_unavailable'
    if isinstance(exc, (ValueError, KeyError, TypeError)):
        return 'invalid_response'
    return 'service_error'


class Service:
    def __init__(self, factory=None):
        self.factory = factory or (lambda: Game(Ollama()))
        self.sessions = {}
        self.lock = threading.Lock()

    def post(self, path, data):
        # Tek GPU üzerinde paralel üretim ve aynı turun iki kez işlenmesini önler.
        with self.lock:
            if path == '/session':
                key = uuid.uuid4().hex
                self.sessions[key] = self.factory()
                return {'session_id': key, 'turn': 0, 'remaining': 5, 'decision': 'pending'}
            key = data.get('session_id')
            if key not in self.sessions:
                raise ValueError('Oturum bulunamadı. Yeni oyun başlatın.')
            if path == '/close':
                del self.sessions[key]
                return {'closed': True}
            if path != '/message':
                raise ValueError('Bilinmeyen adres.')
            message = data.get('message')
            if not isinstance(message, str):
                raise ValueError('message metin olmalı.')
            game = self.sessions[key]
            # Tamamlanan isteğin ağ nedeniyle yeniden gönderilmesinde tur harcanmaz.
            expected = data.get('turn')
            if type(expected) is not int or not 1 <= expected <= 5:
                raise ValueError('turn 1–5 arasında olmalı.')
            if expected <= len(game.records):
                record = game.records[expected - 1]
                if record['player'] != message:
                    raise ValueError('Bu tur daha önce farklı mesajla işlendi.')
            else:
                if expected != len(game.records) + 1:
                    raise ValueError('Tur sırası hatalı.')
                previous_records = list(game.records)
                previous_history = list(getattr(game, 'history', []))
                try:
                    record = game.send(message)
                    game.save(ROOT / 'kayitlar' / ('unity-' + key + '.json'))
                except Exception as exc:
                    # Model/kayıt hatası oyunun turunu veya geçmişini değiştirmez.
                    game.records[:] = previous_records
                    if hasattr(game, 'history'): game.history[:] = previous_history
                    code = failure_code(exc)
                    print('Robot API hatası:', code, type(exc).__name__, flush=True)
                    return {'session_id': key, 'error': code, 'error_code': code,
                        'fallback': True, 'reply': FALLBACK_REPLY, 'turn_consumed': False,
                        'turn': len(game.records), 'remaining': 5-len(game.records),
                        'decision': 'pending', 'door_open': False}
            return {k: record[k] for k in ('turn', 'reply', 'score', 'decision', 'seconds')} | {
                'session_id': key, 'remaining': 5-record['turn'],
                'door_open': record['decision'] == 'allow'}


SERVICE = Service()


class Handler(BaseHTTPRequestHandler):
    def respond(self, status, value):
        body = json.dumps(value, ensure_ascii=False).encode('utf-8')
        self.send_response(status)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.path == '/health':
            self.respond(200, {'ok': True, 'model': CONFIG['temel_model'], 'api_version': '1.1-error-recovery'})
        else:
            self.respond(404, {'error': 'Adres bulunamadı.'})

    def do_POST(self):
        try:
            size = int(self.headers.get('Content-Length', '0'))
            if not 0 < size <= 8192:
                raise ValueError('Geçersiz istek boyutu.')
            data = json.loads(self.rfile.read(size))
            if not isinstance(data, dict):
                raise ValueError('JSON nesnesi bekleniyor.')
            result = SERVICE.post(self.path, data)
            self.respond(503 if result.get('error') else 200, result)
        except (ValueError, KeyError) as exc:
            self.respond(400, {'error': str(exc)})
        except Exception:
            self.log_error('Ollama veya kayıt işlemi başarısız.')
            self.respond(503, {'error': 'Model veya kayıt işlemi başarısız. Sunucu terminalini kontrol edin; aynı mesajla tekrar deneyin.'})


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='WOKEN yerel oyun API')
    parser.add_argument('--port', type=int, default=8765)
    args = parser.parse_args()
    if not 1 <= args.port <= 65535: parser.error('Port 1–65535 aralığında olmalı.')
    try:
        Ollama().check()
    except Exception as exc:
        print('Ollama şu anda hazır değil; oyun API açık kalacak:', type(exc).__name__, flush=True)
    try:
        server = ThreadingHTTPServer(('127.0.0.1', args.port), Handler)
    except OSError as exc:
        raise SystemExit(f'API başlatılamadı: {args.port} portu kullanımda olabilir. {exc}')
    print(f'Unity oyun API hazır: http://127.0.0.1:{args.port} (durdurmak için Ctrl+C)', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
