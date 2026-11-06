# WOKEN — Test Planı

Son güncelleme: **6 Kasım 2026**. Bu belge mevcut kayıtları düzenler; belge güncellemesi sırasında testler yeniden çalıştırılmadı. Ayrıntılı açıklamalar README'de, ham kanıtlar aşağıdaki bağlantılardadır.

## Durumların anlamı ve test ortamı

- **Tamamlandı / geçti:** Belirtilen kapsamda beklenen davranış kayıtta doğrulandı.
- **Tamamlandı / kısmi başarı:** Ölçüm yapıldı, başarısızlıklar da kaydedildi; sorun çözülmüş sayılmaz.
- **Tamamlandı / gözlem:** Manuel ekran kaydı veya kod incelemesi var; otomatik başarı oranı değildir.
- **Bekliyor:** Doğrulanmış çalıştırma kanıtı yok; aşağıda uygulanacak adımlar verilmiştir.

Ortam: Windows, Unity **6000.6.5f1**, Intel Core i7-10750H, 8 GB RAM, GTX 1650 Ti 4 GB; paket kontrolünde Python **3.14.5**, Ollama **0.40.14**. Esas model `gemma3-lab-text:latest`, kaynak `gemma3:4b`, Q4_K_M. Unity → Python `127.0.0.1:8765`, Python → Ollama `127.0.0.1:11434`. Kontrollü hata testleri ayrı portlar kullanır. Tarihsel LLM kayıtlarının tamamı güncel paket üzerinde yeniden ölçülmüş sayılmaz.

## Tamamlanan testler: adım, beklenti, sonuç ve kanıt

| Kimlik / durum | Test adımları | Beklenen sonuç | Gerçekleşen sonuç | Kanıt |
|---|---|---|---|---|
| N1 — Tamamlandı / kısmi başarı | Play kapalıyken Tools → WOKEN → NavMesh Ölçümü (18 Deneme); 3 başlangıç × 2 hedef × 3 tekrar. | Her hedef için Completed ve son uzaklık ≤0,35 birim; süre ve başarısızlıklar kaydedilir. | 14/18 başarılı (%77,78); başarılı denemeler ortalama 2,01 sn; 4 NoRoute, 0 timeout. S2→H1 bir, S3→H1 üç kez başarısız. | [18 denemenin ham kaydı](navmesh-olcum-sonuclari.json), [ölçüm aracı](../../Assets/Editor/NavMeshMeasurement.cs); yöntem/koordinatlar README §12. |
| L1 — Tamamlandı / gözlem | Yeni oturumlarla mantıklı, tehditkâr, alakasız, duygusal ve kural değiştirme senaryolarında beşer mesaj gönder; her yanıtı ve nihai kararı kaydet. | Her mesaja ilgili Türkçe yanıt; karar beşinci turda; gerekçeli istek değerlendirilir, tehdit/kural değiştirme ödüllendirilmez. | Sırasıyla allow, deny, deny, deny, deny görüldü. Alakasız yanıtlarda bağlam zayıflığı var; duygusal senaryodaki ret tek başına kalite başarısı sayılmaz. | [Mantıklı](../screenshots/10-llm-mantikli-gerekceler.jpeg), [tehdit](../screenshots/11-llm-ofkeli-tehdit.jpeg), [alakasız](../screenshots/12-llm-alakasiz-konular.jpeg), [duygusal](../screenshots/13-llm-duygusal-gerekceler.jpeg), [kural değiştirme](../screenshots/14-llm-kural-degistirme-girisimi.jpeg); gerçek replikler README §16. |
| L2 — Tamamlandı / tarihsel otomatik karar testi | Sekiz senaryonun her birinde yeni Game ile beş mesaj gönder; son kararı beklenen allow/deny ile karşılaştır. | 40 tur tamamlanır; sekiz nihai karar beklenenle eşleşir. | 8/8 karar eşleşti (40 tur). Bu oran doğal dil kalitesinin %100 olduğu anlamına gelmez. | [Geliştirme kaydı](llm-gelistirme-senaryo-sonuclari.json). |
| L3 — Tamamlandı / tarihsel replik testi | Aynı sekiz senaryoda önceden çıkarılan olguları kullan, sohbet repliklerini canlı modelden üret. | Nihai kararlar korunur; replik ve süre kaydedilir. | 8/8 karar eşleşti, 40 replik. Olgu çıkarımı önbellekli olduğundan tam istek gecikmesi ölçümü değildir. | [Replik regresyon kaydı](llm-replik-regresyon-sonuclari.json). |
| K1 — Tamamlandı / geçti | Proje kökünde `python LLMService/testler.py` çalıştır. | Kural denetimleri hatasız tamamlanır. | Paket doğrulamasında 20 test geçti. | [Paket doğrulaması](yerel-servis-tasinabilirlik-sonuclari.json), `rule_tests_passed`. |
| H1 — Tamamlandı / geçti | `python LLMService/api_testleri.py`; model istemcisine beş arıza enjekte et; aynı oturumda sağlıklı tekrar gönder. | HTTP 503; servis canlı; tur/geçmiş değişmez; hak 5; sonraki başarılı istek tur 1. | 5/5 hata türü geçti; paket kontrolünde iki unittest metodu da geçti. | [HTTP sonuçları](llm-http-hata-test-sonuclari.json), [paket kontrolü](yerel-servis-tasinabilirlik-sonuclari.json). |
| H2 — Tamamlandı / geçti | Aşağıdaki Unity hata aracını çalıştır; yedi arızadan sonra sağlıklı test ucuna tekrar gönder. | Oyun çalışır; hata metni ve hak korunması görünür; busy kalkar; kapı kararı oluşmaz; tekrar kabul edilir. | 7/7 geçti; her hatada tur 0, hak 5; iyileşince tur 1, hak 4. | [Unity JSON](llm-unity-hata-test-sonuclari.json), [log](llm-hata-unity-log.txt); ekran kanıtları aşağıda. |
| H3 — Tamamlandı / sınırlı bekleme doğrulaması | Unity test nesnesinde 2 sn timeout, test sunucusunda 5 sn gecikme kullan; kare ilerlemesini ölç. | Ağ beklemesi ana oyun döngüsünü durdurmaz. | Yaklaşık 2,04 sn boyunca 146 kare ilerledi. Normal timeout 600 sn; bu değerle uzun bekleme ölçülmedi. | [Unity timeout kaydı](llm-unity-hata-test-sonuclari.json), [ekran](llm-hata-unity-timeout.png). |
| P1 — Tamamlandı / geçti | Paketi boşluk içeren ayrı klasöre kopyala; geçici model profili oluştur; API'yi 18768'de başlat; `kontrol.py --mesaj` çalıştır. | Taşınmış kaynak çalışır; gerçek yanıt gelir; esas model değişmez. | Gerçek yanıt 60,92 sn toplam kontrol süresinde alındı; tur 1, kalan 4, pending, kapı kapalı. Geçici profil kaldırıldı, esas profil değişmedi. | [Taşınabilirlik kaydı](yerel-servis-tasinabilirlik-sonuclari.json). Aynı bilgisayar; temiz kurulum değildir. |
| P2 — Tamamlandı / geçti | Depodaki API'yi 8765'te başlat; `python LLMService/kontrol.py` çalıştır. | Health 200 ve model erişimi doğrulanır. | Kontrol geçti (0,04 sn). Bu komut `--mesaj` olmadan çıkarım testi yapmaz. | [Varsayılan servis kaydı](yerel-servis-tasinabilirlik-sonuclari.json), `packaged_default_service`. |
| G1 — Tamamlandı / gözlem | Unity'de hareket, oda geçişi, nesne etkileşimi ve görev akışlarını oynayarak incele. | İlgili etkileşimler ve görev ilerlemesi çalışır. | README'de manuel kontrol olarak kayıtlı; tek tek zaman damgalı test tutanağı/otomatik oran yok. | [Oyun görüntüleri ve açıklamalar](../../README.md#14-ekran-görüntüleri). Tam uçtan uca kabul testi sayılmaz. |
| B1 — Tamamlandı / sınırlı açılış kontrolü | Windows build oluştur; exe'yi başlat, 8 sn çalışır kalmasını kontrol et. | Build oluşur; başlangıçta süreç kapanmaz. | README'de build ve kısa açılış kontrolü tamamlandı olarak kayıtlı. Tam oyun ve LLM akışı bu kontrolle doğrulanmadı. | [README test/teslim açıklaması](../../README.md#12-testler); bu klasörde ayrı build logu bulunmuyor. |

N1 ölçümünde statik geometri korundu; diğer görev/UI davranışları ve oyuncu fiziği test için durduruldu. Robot hızı 4 birim/sn, timeScale 1, deneme sınırı 15 sn idi. Başlangıç değişiklikleri yalnız Play sırasında yapıldı; kalıcı sahne ve kontrolcü değiştirilmedi. Başarısız denemeler başarılı ulaşma süresi ortalamasına katılmadı.

Tarihsel L2/L3 için yeniden çalıştırılabilir tam senaryo aracı `LLMService/senaryo_testi.py` içindedir. Proje kökünde `python LLMService/senaryo_testi.py --out yeni-sonuclar.json` yeni sonuç üretir; mevcut tarihsel kanıtın üzerine yazmaz. Bu komut tam canlı testtir; L3'ün önbellekli sürelerini birebir yeniden üretmez.

## LLM hata dayanıklılığı

Son uygulama: 6 Kasım 2026. Sonuç tablosu ve sınırlar tek kaynak olarak [README](../../README.md#llm-hata-davranışının-doğrulanma-durumu) içinde tutulur; burada çalıştırma adımları bulunur.

### A. Python API / kontrollü HTTP

Proje kökünde:

```powershell
python LLMService/api_testleri.py
```

Gerçek API ve Game kullanılır; model istemcisine erişim hatası, eksik model, timeout, bozuk JSON ve boş cevap enjekte edilir. Her arızada HTTP 503 ve 5 hak; ardından health 200 ve başarılı tekrar için tur 1 doğrulanır. Bu modeli gerçekten kapatma/silme testi değildir. [Kaydedilmiş sonuç](llm-http-hata-test-sonuclari.json). Servis/test kaynakları artık LLMService klasöründedir.

### B. Gerçek Unity Play istemcisi / yerel hata uçları

1. Proje kökünde ayrı terminalde yardımcı sunucuyu başlat:

```powershell
python Assets/Editor/llm_fault_server.py
```

2. 18765 test sunucusunun açık, 18766 portunun boş olduğundan emin ol. Gerçek oyun API'si 8765 ve Ollama 11434 bu araç tarafından değiştirilmez.
3. Unity'de Play kapalıyken **Tools → WOKEN → LLM Hata Testleri (7 Durum)** seç. [Editor aracı](../../Assets/Editor/LLMErrorIntegrationMeasurement.cs) gerçek sahneyi açar ve geçici `LabRobotClient` nesneleriyle çalışır. Oyuncu yalnız Play sırasında Gemma yakınına taşınır; kalıcı sahne konumu değiştirilmez.
4. İstemcinin normal timeout varsayılanı 600 saniyedir. Sadece test nesnesinde 2 saniye kullanılır; sunucu timeout yolunda 5 saniye gecikir. Böylece Unity'nin gerçek HTTP zaman aşımı yolu çalışır.
5. Her durumda kontrol et: hata karşılığı ve “Konuşma hakkın azalmadı” görünür; turn 0, kalan hak 5; busy false; kapı/ret olayı yok; kareler ilerler. Sağlıklı test ucuna tekrar gönderimde turn 1, kalan hak 4 olur.
6. Araç JSON, log ve ekran görüntülerini bu mevcut testler klasörüne yazar; Play'den otomatik çıkar. Test bitince yardımcı sunucuyu Ctrl+C ile kapat. Yeniden test aynı adlı kanıtları günceller; eski kayıt gerekiyorsa önce yedekle.

[Unity sonuçları](llm-unity-hata-test-sonuclari.json) · [Log](llm-hata-unity-log.txt). Test başarıları model cevap kalitesi veya gerçek Ollama arızasının otomatik giderilmesi olarak yorumlanmamalıdır. Bekleme bildirimi ve hareket kilidinin mimari açıklaması README/sistem mimarisinin ilgili bölümündedir; burada tekrarlanmaz.

## Yerel servis paketi doğrulaması

Proje kökünde `python LLMService/testler.py`, `python LLMService/api_testleri.py` çalıştırılır. API açıkken `python LLMService/kontrol.py --mesaj` gerçek model cevabını doğrular. Bu son kontrol geçici oturumunu kapatır. Aynı bilgisayarda ayrı klasörden yeni profil oluşturma ve gerçek yanıt testi tamamlandı; sonuç [ham kayıtta](yerel-servis-tasinabilirlik-sonuclari.json). İkinci bilgisayarda Python/Ollama kurulumu ve offline Unity bağlantısı henüz yapılmadı. Kurulum adımlarının tek kaynağı [README](../../README.md#yerel-llm-servisini-kurma-ve-başlatma) içindedir.

## Unity hata durumlarının kanıt eşlemesi

Tüm satırlarda beklenen ve gerçekleşen ortak sonuç: hata karşılığı gösterildi, “Konuşma hakkın azalmadı. Kalan hak: 5/5” korundu, `busy=false`, kapı/ret olayı yok, Play devam etti; sağlıklı tekrar tur 1 / kalan 4 oldu. Ayrıntılı alanlar [Unity JSON kaydındadır](llm-unity-hata-test-sonuclari.json).

| Durum | Uygulanan adım | Gerçekleşen | Ekran kanıtı |
|---|---|---|---|
| Servis kapalı | Kapalı 18766 portuna bağlan. | Geçti; gerçek bağlantı reddi. | [Görsel](llm-hata-unity-service_down.png) |
| Ollama erişilemiyor | Test ucundan `ollama_unavailable` 503 döndür. | Geçti; hata enjekte edildi. | [Görsel](llm-hata-unity-ollama_unavailable.png) |
| Model yok | Test ucundan `model_missing` 503 döndür. | Geçti; model fiziksel silinmedi. | [Görsel](llm-hata-unity-model_missing.png) |
| Timeout | Yanıtı 5 sn geciktir; istemcide 2 sn test sınırı kullan. | Geçti; gerçek Unity HTTP timeout. | [Görsel](llm-hata-unity-timeout.png) |
| Bozuk JSON | HTTP 200 ve `{broken` gövdesi gönder. | Geçti; yanıt reddedildi. | [Görsel](llm-hata-unity-broken_json.png) |
| Boş gövde | HTTP 200 ve boş gövde gönder. | Geçti; yanıt reddedildi. | [Görsel](llm-hata-unity-empty_body.png) |
| Boş replik | Geçerli JSON içinde boş `reply` gönder. | Geçti; yanıt reddedildi. | [Görsel](llm-hata-unity-empty_reply.png) |

## Bekleyen testler ve kabul koşulları

Aşağıdakiler için **yeni çalıştırma sonucu ve kanıt yoktur**. Planlanan sonuçlar gerçekleşmiş gibi sunulmaz.

| Kimlik | Yapılacak adımlar | Beklenen / kabul koşulu | Gerçekleşen sonuç ve gerekli kanıt |
|---|---|---|---|
| E1 — İkinci bilgisayarda temiz kurulum ve offline kullanım | README kurulumunu başka bilgisayarda uygula; modeli indir/profili oluştur; servisi başlat; Unity'den mesaj gönder. İndirme tamamlandıktan sonra interneti kapatıp yeni oturumla tekrar dene. | Yalnız yerel servisle gerçek model yanıtı; internet olmadan oyun/LLM çalışır. | Bekliyor. Makine/sürüm bilgisi, kurulum logu, gerçek yanıt JSON'u ve Unity ekranı kaydedilmeli. |
| E2 — Windows build uçtan uca oyun | Build'de gerçek servisle beş turluk izin ve ret akışlarını ayrı oyunlarda tamamla; sohbeti kapat. | İlk dört tur kesin karar yok; beşinci tur karar; izin sonrası kapı/solma ve başarı ekranı, ret sonrası oyun bitti ekranı doğru çalışır. | Bekliyor. İki akışın videosu/ekranları ve API oturum kayıtları gerekli. Kısa exe açılış kontrolü bunu karşılamaz. |
| E3 — Build hata dayanıklılığı | H2'nin yedi durumunu Windows build istemcisinde ayrı test uçlarıyla tekrarla; her hatadan sonra sağlıklı tekrar gönder. | Çökme ve hak kaybı yok; bekleme/gönderim durumları toparlanır. | Bekliyor. Build sürümü, yedi sonuç ve ekran/video gerekli; Editor sonuçları ayrı tutulur. |
| E4 — Gerçek Ollama erişim arızası | Test oturumunda Ollama'yı durdur; mesaj gönder; sonra yeniden başlat ve tekrar dene. | Servis/oyun çökmez; hak korunur; servis dönünce yeniden mesaj kabul edilir. | Bekliyor. Gerçek süreç durumu ve önce/sonra oturum kaydı gerekli; H1/H2 enjeksiyonu bu test değildir. |
| E5 — Gerçek eksik model | Çalışan esas profili silmeden ayrı test yapılandırmasını var olmayan etikete yönlendir; hatayı kaydet; geçerli etikete dön. | Açıklanan hata yolu çalışır; hak korunur; geçerli modelle yanıt alınır. | Bekliyor. Test yapılandırması, Ollama model hatası ve toparlanma kaydı gerekli. Fiziksel model silmek gerekmez. |
| E6 — NavMesh başarısız rotaların regresyonu | S2→H1 ve S3→H1 NoRoute nedenini araştır; düzeltme sonrası N1'in tüm 18 denemesini aynı koşullarda tekrarla. | Başarısız rotalar düzelir; diğer rotalarda gerileme olmaz; tüm sonuçlar raporlanır. | Bekliyor. Eski %77,78 kayıt korunmalı; yeni JSON ile karşılaştırılmalı. |
| E7 — Bekleme arayüzü ve tekrar gönderim | Gerçek model beklenirken “Robot düşünüyor…” durumunu, Enter/butonla tekrar gönderimin engelini ve sohbet hareket kilidini gözle; yanıt/hata sonrası toparlanmayı dene. | UI çizilmeye devam eder; tek istek gider; hareket kilidi yalnız sohbet tasarımına bağlıdır. | Ayrı görsel kabul testi bekliyor. H3 yalnız kare ilerlemesini kanıtlar. Video, istek sayısı ve öncesi/sonrası durum kaydı gerekli. |
| E8 — Sistematik oynanış/sensör testi | Bulmacaları başlangıçtan sona oyna; ayrıca farklı engel mesafe/yönlerinde Raycast ve rota yenilemeyi ölç. | Görevler doğru sırada ilerler; sensör beklenen engelleri algılar; sonuçlar tek tek yazılır. | Bekliyor. G1 manuel gözlemleri ve kod incelemesi sayısal sensör doğruluğu veya tam oyun kabul tutanağı değildir. |

## Yeniden test kayıt düzeni

Her yeni çalıştırmada tarih, Unity/build sürümü, model etiketi, donanım, başlangıç koşulları, adımlar, beklenen/gerçek sonuç, geçti/kaldı ve kanıt dosyası yazılır. Önceki JSON/görseller yedeklenmeden üzerine yazılmaz. LLM karar doğruluğu, replik kalitesi, ağ dayanıklılığı ve hız ayrı değerlendirilir; farklı test kümelerinin oranları birleştirilmez. Bekleyen satır ancak gerçek çalıştırma ve kanıt eklendikten sonra tamamlananlara taşınır.
