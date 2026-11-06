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
