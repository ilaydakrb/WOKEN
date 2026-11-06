from http.server import ThreadingHTTPServer,BaseHTTPRequestHandler
import json,time,socket
class Handler(BaseHTTPRequestHandler):
 def do_POST(self):
  try:
   body=json.loads(self.rfile.read(int(self.headers.get('Content-Length','0'))))
   parts=self.path.strip('/').split('/');mode,endpoint=parts[0],parts[-1]
   code=200
   if endpoint=='session':value={'session_id':'fault-probe','turn':0,'remaining':5,'decision':'pending'}
   elif mode=='timeout':
    time.sleep(5);value={'error':'timeout'};code=503
   elif mode in ('ollama_unavailable','model_missing'):
    value={'error':mode,'reply':'Ne dediğini anlayamadım. Biraz daha açık anlatır mısın?','turn':0,'remaining':5,'decision':'pending','door_open':False};code=503
   else:
    turn=body.get('turn',1)
    value={'session_id':'fault-probe','reply':'' if mode=='empty_reply' else 'Mesajını aldım. Neden ayrılmak istediğini anlatır mısın?','turn':turn,'remaining':5-turn,'score':20,'decision':'pending','door_open':False}
   data=b'{broken' if mode=='broken_json' and endpoint=='message' else b'' if mode=='empty_body' and endpoint=='message' else json.dumps(value,ensure_ascii=False).encode()
   self.send_response(code);self.send_header('Content-Type','application/json; charset=utf-8');self.send_header('Content-Length',str(len(data)));self.end_headers();self.wfile.write(data)
  except (BrokenPipeError,ConnectionResetError,ConnectionAbortedError):pass
 def log_message(self,fmt,*args):print(self.path,fmt%args,flush=True)
# Ayrı kapalı uç gerçekten boş mu? Başka bir hizmeti hata hedefi yapma.
with socket.socket() as probe:
 probe.bind(('127.0.0.1',18766))
server=ThreadingHTTPServer(('127.0.0.1',18765),Handler)
print('FAULT TEST SERVER 127.0.0.1:18765; real Ollama unchanged',flush=True)
try:server.serve_forever()
except KeyboardInterrupt:pass
finally:server.server_close()
