"""Model/profil/API kontrolü; --mesaj ile gerçekten bir LLM cevabı alır."""
import argparse
import json
from pathlib import Path
import sys
import time
import urllib.error
import urllib.request
from robot import Ollama,CONFIG

def request(base,path,data=None,timeout=10):
    req=urllib.request.Request(base.rstrip('/')+path,
        data=None if data is None else json.dumps(data,ensure_ascii=False).encode('utf-8'),
        headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(req,timeout=timeout) as response:return json.load(response)

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--url',default='http://127.0.0.1:8765')
    parser.add_argument('--mesaj',action='store_true',help='Gerçek model cevabıyla kontrol et; ilk açılış uzun sürebilir.')
    parser.add_argument('--out',type=Path)
    args=parser.parse_args()
    started=time.perf_counter()
    result={'model':CONFIG['temel_model'],'api_url':args.url,'message_test':args.mesaj,'ok':False}
    sid=None
    try:
        Ollama().check()
        result['ollama_model_accessible']=True
        health=request(args.url,'/health')
        if not health.get('ok') or health.get('model')!=CONFIG['temel_model']:
            raise ValueError('API modeli bu klasörün proje.json ayarıyla eşleşmiyor.')
        result['health']=health
        if args.mesaj:
            sid=request(args.url,'/session',{})['session_id']
            reply=request(args.url,'/message',{'session_id':sid,'turn':1,
                'message':'Merhaba. Çok susadım ve burada içme suyu yok. Dışarı çıkmak istiyorum.'},timeout=900)
            if not isinstance(reply.get('reply'),str) or not reply['reply'].strip() or reply.get('turn')!=1 or reply.get('remaining')!=4 or reply.get('decision')!='pending' or reply.get('door_open') is not False:
                raise ValueError('Geçerli birinci tur yanıtı alınamadı.')
            result['reply']=reply
        result['ok']=True
    except Exception as exc:
        result['error']=str(exc)
        result['error_type']=type(exc).__name__
    finally:
        if sid is not None:
            try:request(args.url,'/close',{'session_id':sid})
            except Exception:pass
    result['seconds']=round(time.perf_counter()-started,2)
    text=json.dumps(result,ensure_ascii=False,indent=2)
    if args.out:
        args.out.parent.mkdir(parents=True,exist_ok=True);args.out.write_text(text+'\n',encoding='utf-8')
    print(text)
    return 0 if result['ok'] else 1

if __name__=='__main__':sys.exit(main())
