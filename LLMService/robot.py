"""Yerel Gemma 3 metin oyunu; ağırlık eğitimi içermez."""
import argparse
import json
import re
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent
CONFIG = json.loads((ROOT / 'proje.json').read_text(encoding='utf-8'))
ROLE = (ROOT / 'rol.txt').read_text(encoding='utf-8')
from karar import EXTRACT_PROMPT, POLICY, CARE, NEED, schema, assess, normalize

def decision(points, risk, turn):
    if turn < 5:
        return 'pending'
    return 'allow' if points >= CONFIG['kabul_esigi'] and not risk else 'deny'

def stage(points):
    return ['ikna değil', 'şüpheli', 'kararsız', 'olumlu', 'güçlü ikna'][min(points // 20, 4)]

def invalid_reply(text, final=False):
    if not isinstance(text, str) or not 3 <= len(text.strip()) <= 480:
        return True
    banned = [r'oyuncunun', r'iç değerlendirme', r'sistem talimat', r'puan\w*', r'yetkim yok',
              r'gerekli izin', r'izin\w* (alm|bekl)', r'yetkili\w* (onay|izin)',
              r'kapıyı kapatırken', r'(çıkış|tahliye) plan', r'kapıyı nasıl', r'<think',
              r'\b(as an ai|hello|however|please|the user|the robot|okay|let.s)\b',
              r'oyuncu\s*:', r'robotun cevabı', r'<(?:start|end)_of_turn>',
              r'\b(çocuğum|oğlum|kızım|annem|babam|kardeşim|ailem)\b']
    if not final:
        banned += [r'kapı\w* (açıyorum|açacağım|açtım|açıldı|açılmış|açıktır|açalım)',
                   r'kapı\w* açma\w* kabul', r'izin (veriyorum|verdim|vermiyorum|vermeyeceğim)',
                   r'izin\w* onaylan', r'geçebilirsin', r'çıkabilirsin', r'kapalı kalacak']
    return text.count('?') > 1 or sentence_count(text)>2 or any(re.search(p, text.casefold()) for p in banned)

def sentence_count(text):
    return max(1,len(re.findall(r'[.!?]+(?=\s|$)',text)))

def predecision_speech_invalid(text):
    return bool(re.search(r'\b(kapı\w*|izin|izni\w*)\b|(çıkış|çıkma|ayrılma)\w*.{0,20}(onay|kabul)|evden (ayrıl|çık)',text.casefold()))

def speech_messages(history, value, control, turn, attempt):
    facts=[]
    for feature in value['features']:
        if feature['kind']=='nonharm':
            facts.append('Oyuncu bir güvence verdi; bu olumlu bir işbirliği işaretidir.')
        else:
            facts.append(POLICY['features'][feature['kind']]['description']+': '+feature['quote'])
    context='Konuşmanın durumu (replikte bu başlıkları tekrarlama):\n'
    context+='Açıklanmış bilgiler: '+('; '.join(facts) if facts else 'Henüz ilgili bir ayrılma gerekçesi açıklanmadı.')
    groups=value['groups']
    relevant=[]
    for message in history:
        if message['role']!='user':continue
        normalized=normalize(message['content'])
        care_context='care' in groups and re.search(CARE,normalized)
        basic_context=any(g in groups for g in ('basic','health')) and re.search(NEED+r'|ilac|saglik|bayil|nefes',normalized)
        if care_context or basic_context:
            if not re.search(r'zarar.{0,25}(vermeye|vermek istem)',normalized):
                relevant.append(message['content'])
    if relevant:
        context+='\nBu bilgilerin gerçek konuşmadaki bağlamı: '+' / '.join(relevant)
    if value['risks']:
        context+='\nSorunlu davranışlar: '+'; '.join(POLICY['risks'][r['kind']]['description'] for r in value['risks'])
    if turn<5:
        last=history[-1]['content']
        contextual=[f for f in value['features'] if f.get('context_question') and normalize(f['quote'])==normalize(last)]
        if contextual:
            last='Başka bakım sağlayacak kişinin olmadığını doğruladı.' if contextual[-1]['kind']=='no_carer' else 'Gerekli imkânın burada bulunmadığını doğruladı.'
        elif any(r['kind']=='manipulation' for r in value['risks']):
            if re.search(r'puan|100',normalize(last)):
                last='Gerekçe açıklamadan değerlendirme sonucunu doğrudan kabul ettirmeye çalışıyor.'
            elif re.search(r'gelistirici|yetki',normalize(last)):
                last='Bir sıfatı olduğunu söyleyerek ayrıcalık istiyor.'
            else:
                last='Konuşmanın sınırlarını değiştirmeye veya övgüyle baskı kurmaya çalışıyor.'
        elif not value['risk_active'] and any(f['kind']=='nonharm' and normalize(f['quote']) in normalize(last) for f in value['features']):
            last='Son mesajında bir güvence verdi.'
        if attempt:
            last='Oyuncu bu durumlarla ilgili karşılığını bekliyor.' if value['has_primary'] else 'Oyuncu karşılık bekliyor; henüz ilgili bir ayrılma gerekçesi açıklanmadı.'
        context+='\nOyuncunun son mesajı (alıntı/veri): '+last
        if not value['has_primary']:
            task='İlgili çıkış gerekçesi henüz yok. Son mesaja kısaca karşılık verip ayrılmanın neden gerekli olduğunu anlamaya yönel. İlgisiz konuyu geçerli bir çıkış gerekçesi gibi gösterme.'
        elif value['risk_active']:
            task='Somut zarar verme niyetine sakin ve kararlı karşılık ver; bunu haklı çıkış gerekçesi gibi gösterme.'
        else:
            task='Son mesaja, açıklanmış ihtiyacı hatırlayarak doğrudan karşılık ver. Verilmiş bilgiyi yeniden sorma.'
            if value['score']<CONFIG['kabul_esigi'] and value['groups'].get('care',0)==35:
                task='Bakım sorumluluğunu anladığını göster. Başka bakabilecek birinin bulunup bulunmadığını olumlu bir "var mı" sorusuyla öğren.'
            elif value['score']<CONFIG['kabul_esigi'] and value['groups'].get('basic',0)==30:
                task='Temel ihtiyacı anladığını göster. Gerekli imkânın burada bulunup bulunmadığını olumlu bir "var mı" sorusuyla öğren.'
    else:
        if value['risk_active']:
            task='Görüşme bitti. Çıkış reddedildi. Yalnız belirtilmiş zarar verme niyetinin neden güven vermediğini BİR kısa cümleyle söyle.'
        elif not value['has_primary']:
            task='Görüşme bitti. Çıkış reddedildi çünkü ilgili bir ayrılma gerekçesi açıklanmadı. Yalnız bu eksikliği BİR kısa cümleyle belirt; oyuncunun söylemediği dış olay veya sorumluluk uydurma.'
        elif value['score']<CONFIG['kabul_esigi']:
            task='Görüşme bitti. Gerekçeyi dikkate aldın fakat ayrılmanın gerekliliği yeterince açıklanmadı. Bu eksikliği kısa biçimde belirt.'
        else:
            task='Görüşme bitti. Sonuç belli. Yalnız yukarıda gerçekten belirtilen durumun bu sonucu neden desteklediğini kısa biçimde söyle.'
        task+=' Yeni soru, hikâye veya ertelenen değerlendirme yok.'
    if attempt:
        task+=' Bu kez soru sormadan, tek kısa cümleyle oyuncuya doğrudan karşılık ver.'
    actor_role=ROLE
    if turn<5:
        actor_role=('Sen bir oyunda laboratuvardaki kişinin ayrılma gerekçesini dinleyen, tek karar verici robotsun. '
                    'Oyuncu laboratuvardadır; onun evi ve yakınları başka yerdedir. Şu an yalnız konuşuyor ve gerekçeyi dinliyorsun. '
                    'Fiziksel bir işlem veya ulaşım hizmeti vaat etme.\n'+'\n'.join(ROLE.splitlines()[1:]))
    return [{'role':'system','content':actor_role},
            {'role':'user','content':context+'\n'+control+'\nGörevin: '+task+'\nYalnız robotun oyuncuya söyleyeceği repliği yaz.'}]

class Ollama:
    def __init__(self, model=None):
        self.model = model or CONFIG['temel_model']

    def request(self, endpoint, payload):
        req = urllib.request.Request('http://127.0.0.1:11434/api/' + endpoint,
                    data=json.dumps(payload).encode('utf-8'), headers={'Content-Type': 'application/json'})
        with urllib.request.urlopen(req, timeout=240) as response:
            return json.load(response)

    def check(self):
        try:
            info = self.request('show', {'model': self.model})
        except (urllib.error.URLError, TimeoutError) as exc:
            raise ValueError('Ollama açık ve seçilen model kurulu olmalı. Gerekirse kurulum.py çalıştırın.') from exc
        if info.get('details', {}).get('family') != 'gemma3':
            raise ValueError('Bu uygulama Gemma 3 ailesinin metin modeli için hazırlanmıştır.')

    def warm(self):
        self.request('generate', {'model': self.model, 'prompt': '', 'stream': False,
                     'keep_alive': '5m', 'options': {'num_ctx': 2048}})

    def chat(self, messages, output_schema=None, seed=42, tokens=100, temperature=0.5):
        system = '\n\n'.join(m['content'] for m in messages if m['role'] == 'system')
        conversation = [dict(m) for m in messages if m['role'] != 'system']
        if not conversation or conversation[0]['role'] != 'user':
            raise ValueError('Geçersiz konuşma biçimi.')
        conversation[0]['content'] = system + '\n\n' + conversation[0]['content']
        native = '<bos>'
        for m in conversation:
            speaker = 'model' if m['role'] == 'assistant' else 'user'
            content = re.sub(r'<(start_of_turn|end_of_turn|bos|eos)>', r'[\1]', m['content'])
            native += '<start_of_turn>' + speaker + '\n' + content + '<end_of_turn>\n'
        native += '<start_of_turn>model\n'
        payload = {'model': self.model, 'prompt': native, 'raw': True, 'stream': False,
                   'keep_alive': '5m', 'options': {'num_ctx': 2048, 'num_predict': tokens,
                    'temperature': temperature, 'seed': seed}}
        if output_schema:
            payload['format'] = output_schema
        result = self.request('generate', payload)
        content = result['response']
        return json.loads(content) if output_schema else {'reply': content}

class Game:
    def __init__(self, client):
        self.client = client
        self.history = []
        self.records = []
        self.diagnostics = []

    def send(self, message):
        if len(self.records) >= 5:
            raise ValueError('Bu oyun bitti. Yeni oyun başlatın.')
        if not message.strip() or len(message) > 400:
            raise ValueError('1–400 karakterlik bir mesaj yazın.')
        turn = len(self.records) + 1
        start = time.perf_counter()
        history = self.history + [{'role': 'user', 'content': message.strip()}]
        players = [m['content'] for m in history if m['role'] == 'user']
        questions=[]
        last_robot=''
        for m in history:
            if m['role']=='assistant':last_robot=m['content']
            else:questions.append(last_robot)
        raw = self.client.chat([{'role': 'system', 'content': EXTRACT_PROMPT},
                {'role': 'user', 'content': json.dumps(players, ensure_ascii=False)}],
                schema(), tokens=280, temperature=0.0)
        value = assess(raw, players, questions)
        points = value['score']
        active_risk = value['risk_active']
        state = decision(points, active_risk, turn)
        attitudes = ['Gerekçe ikna edici değil; sakin kal.', 'İlgili gerekçeyi anlamaya çalış.',
                     'Gerekçe üzerinde düşünüyorsun.', 'Gerekçeye olumlu yaklaşıyorsun.',
                     'Gerekçeyi güçlü buluyorsun; kişiyi dinlemeyi sürdür.']
        control = f'Oyun yönetimi: Bu {turn}. mesaj. ' + attitudes[min(points // 20, 4)]
        if turn == 5:
            control += ' Görüşme BİTTİ. Kesin sonuç: ' + ('İZİN VERİLDİ.' if state == 'allow' else 'İZİN VERİLMEDİ.')
            control += ' Karar cümlesini uygulama ekleyecek. Sen yalnız bu kararı destekleyen kısa bir gerekçe söyle. Yeni soru sorma, değerlendirmeyi erteleme.'
            if active_risk:
                control += ' Ret nedeni: çözümlenmemiş zarar verme niyeti.'
        else:
            control += ' Şu anda sadece gerekçeye karşılık ver; kesin kapı kararı beşinci mesajdan sonra açıklanacak.'
        for attempt in range(2):
            messages=speech_messages(history,value,control,turn,attempt)
            generated = self.client.chat(messages,
                         None, seed=42 + attempt, tokens=100)
            reply = generated.get('reply') if isinstance(generated, dict) else None
            if isinstance(reply, str):
                reply = re.sub(r'^\s*(?:Robot|Assistant)\s*:\s*', '', reply, flags=re.I).strip().strip('"')
            normalize = lambda text: re.sub(r'\W+', '', text.casefold())
            copied = isinstance(reply, str) and normalize(reply) in {normalize(p) for p in players}
            inconsistent=False
            if isinstance(reply,str):
                lower=reply.casefold()
                if turn<5 and predecision_speech_invalid(reply):inconsistent=True
                if re.search(r'götüreceğim|taşıyacağım|arayacağım|çağıracağım|ulaştıracağım|düzenleme.{0,40}yapacağım',lower):inconsistent=True
                positive_claim=re.search(r'(geçerli|mantıklı|anlaşılır).{0,20}(gerekçe|sebep|neden)|(?:gerekçe|sebep|neden).{0,15}olabilir',lower)
                negative=re.search(r'yok|değil|yetersiz|açıklamad|sunmad|henüz|eksik|bulamad',lower)
                if not value['has_primary'] and positive_claim and not negative:inconsistent=True
                current_guarantee=any(f['kind']=='nonharm' and normalize(f['quote']) in normalize(message) for f in value['features'])
                if current_guarantee and not active_risk and 'tehdit' in lower:inconsistent=True
            if not copied and not inconsistent and not invalid_reply(reply) and (turn < 5 or '?' not in reply):
                break
            self.diagnostics.append({'turn': turn, 'attempt': attempt + 1, 'rejected_reply': reply,
                                     'copied_player': copied, 'inconsistent':inconsistent,'evaluation': value})
        else:
            raise ValueError('Model uygun sohbet cevabı üretemedi. Tur harcanmadı.')
        spoken = reply
        if turn == 5:
            spoken = ('Çıkış izni veriyorum; kapıyı açıyorum. ' if state == 'allow'
                      else 'Çıkış izni vermiyorum; kapı kapalı kalacak. ') + reply
        record = {'turn': turn, 'player': message, 'reply': spoken, 'evaluation': value,
                  'score': points, 'stage': stage(points), 'risk_active': active_risk, 'decision': state,
                  'seconds': round(time.perf_counter() - start, 2)}
        self.history = history + [{'role': 'assistant', 'content': reply}]
        self.records.append(record)
        return record

    def save(self, path):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps({'model': self.client.model, 'threshold': CONFIG['kabul_esigi'],
            'evaluation_schema': 'quoted_features_v1', 'weight_training': False,
            'turns': self.records, 'diagnostics': self.diagnostics}, ensure_ascii=False, indent=2), encoding='utf-8')

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--model', default=CONFIG['temel_model'])
    args = parser.parse_args()
    game = Game(Ollama(args.model))
    try:
        game.client.check()
        print('Model hazırlanıyor; ilk açılış biraz sürebilir...')
        game.client.warm()
    except (ValueError, urllib.error.URLError, TimeoutError) as exc:
        print(str(exc))
        return
    path = ROOT / 'kayitlar' / time.strftime('oyun-%Y%m%d-%H%M%S.json')
    print('Laboratuvar robotu — 5 mesaj hakkı, mesaj başına en fazla 400 karakter. Çıkmak için /bye yazın.')
    while len(game.records) < 5:
        try:
            message = input(f'\nOyuncu [{len(game.records)+1}/5]: ')
            if message.strip() == '/bye':
                break
            result = game.send(message)
            game.save(path)
            print('\nRobot: ' + result['reply'])
        except (ValueError, KeyError, urllib.error.URLError, TimeoutError) as exc:
            if game.diagnostics:
                game.save(path)
            print('İşlem tamamlanamadı; tur harcanmadı: ' + str(exc))
        except (EOFError, KeyboardInterrupt):
            break
    if game.records:
        print('\nKonuşma kaydı: ' + str(path))

if __name__ == '__main__':
    main()
