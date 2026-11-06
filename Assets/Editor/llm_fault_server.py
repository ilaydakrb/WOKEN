"""Controlled localhost errors and dropped-response reconciliation, no real model."""
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
import json
import socket
import threading
import time
import uuid

sessions = {}
lock = threading.Lock()

class Handler(BaseHTTPRequestHandler):
    def do_POST(self):
        try:
            body = json.loads(self.rfile.read(int(self.headers.get('Content-Length', '0'))))
            mode, endpoint = self.path.strip('/').split('/')
            drop = False
            with lock:
                code = 200
                if endpoint == 'session':
                    key = uuid.uuid4().hex
                    sessions[key] = {'records': [], 'sync_failures': 0, 'dropped': False}
                    value = {'session_id': key, 'turn': 0, 'remaining': 5, 'decision': 'pending'}
                else:
                    key = body['session_id']
                    session = sessions[key]
                    records = session['records']
                    n = body.get('turn', 1)
                    if endpoint == 'sync':
                        if mode.startswith('lost_') and session['sync_failures'] == 0:
                            session['sync_failures'] += 1
                            code, value = 503, {'error': 'service_unavailable'}
                        elif n <= len(records):
                            value = dict(records[n-1], sync_state='completed')
                        else:
                            value = {'session_id': key, 'sync_state': 'not_processed',
                                     'turn': len(records), 'remaining': 5-len(records),
                                     'decision': 'pending', 'door_open': False}
                    else:
                        if mode == 'timeout':
                            time.sleep(5)
                        if mode in ('timeout', 'ollama_unavailable', 'model_missing'):
                            code, value = 503, {'session_id': key, 'error': mode, 'error_code': mode,
                                'fallback': True, 'turn_consumed': False, 'turn': len(records),
                                'remaining': 5-len(records), 'decision': 'pending', 'door_open': False}
                        else:
                            value = {'session_id': key, 'player': body.get('message', ''),
                                'reply': '' if mode == 'empty_reply' else 'Mesajını aldım. Ayrılma nedenini dinliyorum.',
                                'turn': n, 'remaining': 5-n, 'score': 70,
                                'decision': 'allow' if n == 5 else 'pending', 'door_open': n == 5}
                            should_drop = mode in ('lost_response', 'lost_uncommitted') or mode == 'lost_final' and n == 5
                            if should_drop and not session['dropped']:
                                session['dropped'] = True
                                if mode != 'lost_uncommitted': records.append(value)
                                drop = True
                            elif mode not in ('broken_json', 'empty_body', 'empty_reply'):
                                records.append(value)
            if drop:
                self.close_connection = True
                self.connection.shutdown(socket.SHUT_RDWR)
                self.connection.close()
                return
            data = (b'{broken' if mode == 'broken_json' and endpoint == 'message'
                    else b'' if mode == 'empty_body' and endpoint == 'message'
                    else json.dumps(value, ensure_ascii=False).encode('utf-8'))
            self.send_response(code)
            self.send_header('Content-Type', 'application/json; charset=utf-8')
            self.send_header('Content-Length', str(len(data)))
            self.end_headers()
            self.wfile.write(data)
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            pass
    def log_message(self, fmt, *args):
        print(self.path, fmt % args, flush=True)

if __name__ == '__main__':
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 18766))
    server = ThreadingHTTPServer(('127.0.0.1', 18765), Handler)
    print('FAULT TEST SERVER 127.0.0.1:18765; production model unchanged', flush=True)
    try: server.serve_forever()
    except KeyboardInterrupt: pass
    finally: server.server_close()
