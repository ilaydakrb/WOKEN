import tempfile
import unittest
from pathlib import Path
import api_sunucu as api
import json
import threading
import urllib.error
import urllib.request
from robot import Game

class FakeGame:
    def __init__(self): self.records = []
    def send(self, message):
        n = len(self.records)+1
        r = dict(turn=n, player=message, reply='Yanıt', score=70,
                 decision='allow' if n == 5 else 'pending', seconds=0)
        self.records.append(r)
        return r
    def save(self, path): pass

class ApiTests(unittest.TestCase):
    def test_http_model_failures_do_not_spend_turn(self):
        class Client:
            model = 'test-double'
            def __init__(self, mode): self.mode = mode
            def chat(self, messages, output_schema=None, **kwargs):
                if self.mode == 'closed': raise urllib.error.URLError(ConnectionRefusedError())
                if self.mode == 'missing': raise urllib.error.HTTPError('local',404,'model missing',None,None)
                if self.mode == 'timeout': raise TimeoutError('simulated timeout')
                if self.mode == 'broken': return json.loads('{broken')
                if output_schema: return {'features': [], 'risks': []}
                if self.mode == 'empty': return {'reply': ''}
                return {'reply': 'Neden ayrılmak istediğini anlatır mısın?'}
        report=[]
        old_service=api.SERVICE
        try:
            scratch=api.ROOT/'test-output'
            scratch.mkdir(exist_ok=True)
            with tempfile.TemporaryDirectory(dir=scratch) as directory:
                old_root=api.ROOT
                api.ROOT=Path(directory)
                for mode,code in [('closed','ollama_unavailable'),('missing','model_missing'),
                                  ('timeout','timeout'),('broken','invalid_response'),('empty','invalid_response')]:
                    with self.subTest(mode=mode):
                        client=Client(mode)
                        api.SERVICE=api.Service(lambda: Game(client))
                        server=api.ThreadingHTTPServer(('127.0.0.1',0),api.Handler)
                        worker=threading.Thread(target=server.serve_forever,daemon=True);worker.start()
                        base='http://127.0.0.1:'+str(server.server_port)
                        def post(path,data):
                            req=urllib.request.Request(base+path,data=json.dumps(data).encode(),headers={'Content-Type':'application/json'})
                            try:
                                with urllib.request.urlopen(req,timeout=3) as r:return r.status,json.load(r)
                            except urllib.error.HTTPError as e:return e.code,json.load(e)
                        try:
                            _,session=post('/session',{})
                            sid=session['session_id']
                            payload={'session_id':sid,'message':'Merhaba','turn':1}
                            status,result=post('/message',payload)
                            self.assertEqual(status,503)
                            self.assertEqual(result['error_code'],code)
                            self.assertFalse(result['turn_consumed'])
                            self.assertEqual(result['remaining'],5)
                            self.assertEqual(result['reply'],api.FALLBACK_REPLY)
                            self.assertEqual(api.SERVICE.sessions[sid].history,[])
                            self.assertEqual(api.SERVICE.sessions[sid].records,[])
                            self.assertEqual(urllib.request.urlopen(base+'/health',timeout=3).status,200)
                            client.mode='ok'
                            status,retry=post('/message',payload)
                            self.assertEqual(status,200)
                            self.assertEqual(retry['turn'],1)
                            self.assertEqual(retry['remaining'],4)
                            report.append({'simulated_failure':mode,'error_code':code,'http_status':503,
                                'server_alive':True,'remaining_after_error':5,'retry_turn':1,'passed':True})
                        finally:
                            server.shutdown();server.server_close();worker.join()
                (old_root/'hata-test-sonuclari.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
        finally:
            api.SERVICE=old_service
            api.ROOT=old_root

    def test_sync_recovers_committed_turn_before_revised_message(self):
        s = api.Service(FakeGame)
        sid = s.post('/session', {})['session_id']
        original = dict(session_id=sid, message='Original message', turn=1)
        expected = s.post('/message', original)  # Simulate losing its response.
        recovered = s.post('/sync', original)
        self.assertEqual(recovered['sync_state'], 'completed')
        self.assertEqual(recovered['reply'], expected['reply'])
        self.assertEqual(recovered['player'], original['message'])
        self.assertEqual(len(s.sessions[sid].records), 1)
        revised = s.post('/message', dict(session_id=sid, message='Revised message', turn=2))
        self.assertEqual(revised['turn'], 2)
        self.assertEqual(revised['remaining'], 3)

    def test_sync_unprocessed_turn_allows_revised_message(self):
        s = api.Service(FakeGame)
        sid = s.post('/session', {})['session_id']
        state = s.post('/sync', dict(session_id=sid, message='Lost before processing', turn=1))
        self.assertEqual(state['sync_state'], 'not_processed')
        self.assertEqual(state['remaining'], 5)
        revised = s.post('/message', dict(session_id=sid, message='Revised message', turn=1))
        self.assertEqual(revised['turn'], 1)
        self.assertEqual(revised['remaining'], 4)

    def test_sync_final_decision_and_session_isolation(self):
        s = api.Service(FakeGame)
        sid = s.post('/session', {})['session_id']
        other = s.post('/session', {})['session_id']
        for n in range(1, 6):
            payload = dict(session_id=sid, message='Message '+str(n), turn=n)
            s.post('/message', payload)
        for _ in range(2):
            state = s.post('/sync', payload)
            self.assertEqual(state['decision'], 'allow')
            self.assertTrue(state['door_open'])
            self.assertEqual(state['remaining'], 0)
        self.assertEqual(len(s.sessions[sid].records), 5)
        self.assertEqual(len(s.sessions[other].records), 0)
        with self.assertRaises(ValueError):
            s.post('/sync', dict(session_id=sid, message='Different', turn=5))

    def test_http_response_dropped_after_commit_is_recoverable(self):
        class DropResponse(api.Handler):
            def respond(self, status, value):
                if self.path == '/message' and value.get('turn') == 1:
                    self.close_connection = True
                    self.connection.shutdown(2)
                    self.connection.close()
                    return
                super().respond(status, value)
        old_service = api.SERVICE
        api.SERVICE = api.Service(FakeGame)
        server = api.ThreadingHTTPServer(('127.0.0.1', 0), DropResponse)
        worker = threading.Thread(target=server.serve_forever, daemon=True)
        worker.start()
        base = 'http://127.0.0.1:'+str(server.server_port)
        def post(path, data):
            req = urllib.request.Request(base+path, data=json.dumps(data).encode(), headers={'Content-Type':'application/json'})
            with urllib.request.urlopen(req, timeout=3) as response: return json.load(response)
        try:
            sid = post('/session', {})['session_id']
            payload = dict(session_id=sid, message='Original', turn=1)
            with self.assertRaises(Exception): post('/message', payload)
            recovered = post('/sync', payload)
            self.assertEqual(recovered['turn'], 1)
            self.assertEqual(recovered['sync_state'], 'completed')
            next_reply = post('/message', dict(session_id=sid, message='Revised', turn=2))
            self.assertEqual(next_reply['turn'], 2)
            self.assertEqual(len(api.SERVICE.sessions[sid].records), 2)
        finally:
            server.shutdown(); server.server_close(); worker.join()
            api.SERVICE = old_service

    def test_sync_waits_until_inflight_turn_finishes(self):
        entered, release, synced = threading.Event(), threading.Event(), threading.Event()
        class SlowGame(FakeGame):
            def send(self, message):
                entered.set()
                if not release.wait(3): raise TimeoutError('test coordination timeout')
                return super().send(message)
        s = api.Service(SlowGame)
        sid = s.post('/session', {})['session_id']
        payload = dict(session_id=sid, message='Original', turn=1)
        results = []
        worker = threading.Thread(target=lambda: s.post('/message', payload))
        def synchronize():
            results.append(s.post('/sync', payload))
            synced.set()
        reader = threading.Thread(target=synchronize)
        worker.start()
        try:
            self.assertTrue(entered.wait(2))
            reader.start()
            self.assertFalse(synced.wait(.05))
        finally:
            release.set(); worker.join(3)
            if reader.ident is not None: reader.join(3)
        self.assertTrue(synced.is_set())
        self.assertEqual(results[0]['sync_state'], 'completed')
        self.assertEqual(len(s.sessions[sid].records), 1)

    def test_session_retry_final_and_isolation(self):
        s=api.Service(FakeGame)
        a=s.post('/session', {})['session_id']
        b=s.post('/session', {})['session_id']
        payload=dict(session_id=a,message='Merhaba',turn=1)
        first=s.post('/message',payload)
        self.assertEqual(first,s.post('/message',payload))
        self.assertEqual(len(s.sessions[a].records),1)
        self.assertEqual(len(s.sessions[b].records),0)
        with self.assertRaises(ValueError):
            s.post('/message',dict(session_id=a,message='Farklı',turn=1))
        for n in range(2,6):
            result=s.post('/message',dict(session_id=a,message='Mesaj',turn=n))
            self.assertEqual(result['door_open'],n==5)
        self.assertEqual(result['remaining'],0)
        s.post('/close',dict(session_id=a))
        with self.assertRaises(ValueError): s.post('/message',payload)

if __name__=='__main__': unittest.main()
