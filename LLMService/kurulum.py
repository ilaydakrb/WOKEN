"""Kurulu resmi Gemma metin GGUF'ünden taşınabilir yerel profil oluşturur."""
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import urllib.error
import urllib.request

ROOT=Path(__file__).resolve().parent

def show(model):
    request=urllib.request.Request('http://127.0.0.1:11434/api/show',data=json.dumps({'model':model}).encode(),headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(request,timeout=30) as response:return json.load(response)

def first_weights(info):
    match=re.search(r'^FROM\s+(.+)$',info.get('modelfile',''),re.M)
    if not match:raise RuntimeError('Modelin metin ağırlık yolu bulunamadı.')
    return Path(match.group(1).strip().strip('"'))

def main():
    config=json.loads((ROOT/'proje.json').read_text(encoding='utf-8'))
    source=config.get('kaynak_model','gemma3:4b')
    profile=config['profil_modeli']
    if profile==source or not profile.startswith('gemma3-'):
        raise RuntimeError('Kaynak modele dokunmadan ayrı gemma3- profili bekleniyor.')
    executable=shutil.which('ollama')
    if not executable:
        candidate=Path(os.environ.get('LOCALAPPDATA',''))/'Programs/Ollama/ollama.exe'
        if candidate.is_file():executable=str(candidate)
    if not executable:raise RuntimeError('Ollama komutu bulunamadı. Ollama kurup terminali yeniden açın.')
    try:info=show(source)
    except urllib.error.HTTPError as exc:
        if exc.code==404:raise RuntimeError(f'Kaynak model yok. Önce ollama pull {source} çalıştırın.') from exc
        raise
    except (urllib.error.URLError,TimeoutError) as exc:
        raise RuntimeError('Ollama açık olmalı; 127.0.0.1:11434 adresine erişilemedi.') from exc
    if info.get('details',{}).get('family')!='gemma3':raise RuntimeError('Kaynak Gemma 3 değil.')
    weights=first_weights(info)
    with weights.open('rb') as file:
        if file.read(4)!=b'GGUF':raise RuntimeError('Kaynak metin ağırlığı GGUF değil.')
    try:existing=show(profile)
    except urllib.error.HTTPError as exc:
        if exc.code!=404:raise
        existing=None
    if existing is not None:
        if existing.get('details',{}).get('family')!='gemma3' or first_weights(existing).resolve()!=weights.resolve():
            raise RuntimeError('Mevcut profil farklı ağırlıklara ait; üzerine yazılmadı.')
        print(f'{profile} zaten doğru metin ağırlıklarını kullanıyor; değiştirilmedi.')
        return
    license_block=re.search(r'LICENSE\s+""".*?"""',info.get('modelfile',''),re.S)
    if not license_block:raise RuntimeError('Kaynak modelin lisans metni bulunamadı; profil oluşturulmadı.')
    # İlk FROM metin modelidir; görüntü projektörü eklenmez. Kişisel yol Git'e yazılmaz.
    generated=ROOT/'Modelfile.generated'
    generated.write_text('FROM '+json.dumps(weights.as_posix(),ensure_ascii=False)+'\nPARAMETER num_ctx 2048\nPARAMETER temperature 0.5\n'+license_block.group(0)+'\n',encoding='utf-8')
    subprocess.run([executable,'create',profile,'-f',str(generated)],check=True)
    if show(profile).get('details',{}).get('family')!='gemma3':raise RuntimeError('Profil doğrulanamadı.')
    print(f'{profile} oluşturuldu. API: python api_sunucu.py')

if __name__=='__main__':
    try:main()
    except (RuntimeError,OSError,subprocess.SubprocessError,urllib.error.URLError,ValueError) as exc:
        raise SystemExit(str(exc))
