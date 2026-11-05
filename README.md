# WOKEN — Yapay Zekâ Destekli 2D Bulmaca ve Kaçış Oyunu

WOKEN, Unity ve C# ile geliştirilen, keşif, bulmaca çözme ve yapay zekâ destekli etkileşimleri hedefleyen 2D bir kaçış oyunudur. Oyuncu, terk edilmiş bir laboratuvarda uyanır; odaları keşfederek görevleri tamamlamaya, gizli bilgileri bulmaya ve çıkış yoluna ulaşmaya çalışır.

Oyunda oyuncuya eşlik eden otonom bir yardımcı robot bulunur. Robot oyuncuyu takip eder, NavMesh tabanlı navigasyon kullanır ve senaryodaki bazı görevleri yerine getirir. İlerleyen geliştirme aşamasında oyuncunun Gemma adlı karakterle Ollama üzerinden yerel LLM kullanarak Türkçe diyalog kurması hedeflenir.

Proje, Kocaeli Üniversitesi Bilişim Sistemleri Mühendisliği bölümü kapsamında iki kişilik bir ekip tarafından geliştirildi.

---

## İçindekiler

1. [Projenin Amacı](#1-projenin-amacı)
2. [Oyun Hikâyesi](#2-oyun-hikâyesi)
3. [Temel Özellikler](#3-temel-özellikler)
4. [Kullanılan Teknolojiler](#4-kullanılan-teknolojiler)
5. [Sistem Mimarisi](#5-sistem-mimarisi)
6. [Oyun Mekanikleri](#6-oyun-mekanikleri)
7. [Laboratuvar Haritası ve Bulmacalar](#7-laboratuvar-haritası-ve-bulmacalar)
8. [Yardımcı Robot Sistemi](#8-yardımcı-robot-sistemi)
9. [Gemma ile Yerel LLM Diyaloğu](#9-gemma-ile-yerel-llm-diyaloğu)
10. [Güncel Proje Durumu](#10-güncel-proje-durumu)
11. [Kurulum ve Çalıştırma](#11-kurulum-ve-çalıştırma)
12. [Testler](#12-testler)
13. [Karşılaşılan Sorunlar ve Çözümleri](#13-karşılaşılan-sorunlar-ve-çözümleri)
14. [Ekran Görüntüleri](#14-ekran-görüntüleri)
15. [Bilinen Eksikler](#15-bilinen-eksikler)
16. [Proje Çıktıları](#16-proje-çıktıları)
17. [Geliştiriciler](#17-geliştiriciler)
18. [Lisans ve Kullanım](#18-lisans-ve-kullanım)

## 1. Projenin Amacı

WOKEN, bulmaca ve kaçış oyunu mekaniklerini robot davranışları ve yapay zekâ destekli diyalog hedefiyle bir araya getirdi. Oyuncunun laboratuvarı keşfetmesi, nesnelerle etkileşime girmesi, yardımcı robotla iş birliği yapması ve görevleri tamamlayarak ilerlemesi amaçlandı.

Projenin başlıca hedefleri şunlardı:

- Unity üzerinde oynanabilir bir 2D laboratuvar ortamı geliştirmek.
- Keşif, nesne etkileşimi ve bulmaca çözme mekaniklerini bir araya getirmek.
- NavMesh tabanlı navigasyonla otonom robot davranışları oluşturmak.
- Robotun oyuncuyu takip etmesini ve senaryodaki görevleri yerine getirmesini sağlamak.
- Yerel bir dil modeliyle Gemma karakteri için dinamik Türkçe diyalog sistemi geliştirmek.
- NPC'nin farklı duygu durumlarına göre tepkiler üretmesini sağlamak.
- Yapay zekâ yanıtlarını oyun mekanikleriyle kontrollü biçimde ilişkilendirmek.

## 2. Oyun Hikâyesi

Oyuncu, terk edilmiş bir bilim laboratuvarında uyanır. Laboratuvardan çıkabilmek için odaları keşfetmesi, ipuçlarını değerlendirmesi ve karşılaştığı bulmacaları çözmesi gerekir.

Oyuncuya eşlik eden yardımcı robot, oyuncuyu takip eder ve belirli görevlerde destek olur. Depo şifresini bulma, bataryayı taşıma ve laboratuvar mekanizmalarını çalıştırma gibi görevler ilerleyişte rol oynar.

Oyuncu; bilgisayar arşivleri, gizli çekmeceler, renkli düğmeler ve batarya ile çalışan elektrikli kapı gibi etkileşimlerle karşılaşır. İlerleyen aşamalarda Gemma ile konuşarak laboratuvardan çıkış için onu ikna etmesi hedeflenir.

## 3. Temel Özellikler

### Oyuncu ve Etkileşim Sistemi

- WASD ve yön tuşlarıyla hareket.
- Laboratuvar alanında keşif.
- Nesne adlarını ve kısa açıklamaları görüntüleme.
- Yakındaki nesnelerle klavye üzerinden etkileşim.
- Görev ve bulmaca ilerleme koşulları.

### Yardımcı Robot

- Oyuncuyu otomatik takip etme.
- NavMesh tabanlı rota takibi.
- Hedef noktalara görev amacıyla hareket etme.
- Görev tamamlanınca oyuncuyu yeniden takip etme.
- Senaryo mesajlarıyla oyuncuyu yönlendirme.
- Batarya taşıma ve özel geçit görevlerine katılma.

### Laboratuvar ve Bulmacalar

- Birden fazla oda ve kat içeren laboratuvar ortamı.
- Depo şifresi ve bilgisayar arşivi gibi keşif öğeleri.
- Renkli düğmeler ve gizli çekmece bulmacası.
- Batarya ile çalışan elektrikli kapı mekanizması.
- Görev koşullarına bağlı oda geçişleri.

### Gemma ile Yapay Zekâ Diyaloğu

- Ollama üzerinden yerel LLM kullanımı.
- Gemma karakteriyle Türkçe yazılı diyalog.
- Oyuncu mesajlarına göre dinamik yanıt üretimi.
- Beş farklı duygu durumuna göre tepki.
- Konuşma geçmişini bağlamda kullanma.
- JSON biçiminde yapılandırılmış yanıtlar.
- Model yanıtlarından doğabilecek oyun olaylarını doğrulama.
- Bağlantı hataları ve zaman aşımını yönetme.

## 4. Kullanılan Teknolojiler

| Teknoloji | Kullanım amacı |
|---|---|
| Unity 6000.6.5f1 | 2D oyun, sahne ve mekanik geliştirme |
| C# | Oyuncu, robot, görev ve etkileşim sistemleri |
| Unity Input System | Klavye ve fare girdileri |
| NavMeshPlus | 2D ortamda NavMesh kullanımı |
| AI Navigation | NavMesh tabanlı navigasyon altyapısı |
| Unity UI | Oyun içi arayüzler ve mesajlar |
| Ollama ve Gemma | Yerel LLM diyaloğu için planlanan teknoloji |
| JSON | Planlanan LLM yanıt yapısı |
| Git ve GitHub | Sürüm kontrolü ve ekip çalışması |


## 5. Sistem Mimarisi

Oyun alanı ve temel sistemler `WokenLevelBootstrap` tarafından oluşturuldu ve birbirine bağlandı. Oyuncu hareketi, robot navigasyonu, görev akışı ve arayüz sorumlulukları farklı bileşenlere ayrıldı.

```text
                         WOKEN
                           |
                  Unity Oyun Uygulaması
                           |
       +-------------------+-------------------+
       |                   |                   |
   Oyuncu Sistemi    Yardımcı Robot       Etkileşim UI'ları
       |                   |                   |
   Input System       NavMeshAgent       Şifre, batarya,
   Rigidbody2D        Görev ve takip      laptop, defter
       |                   |                   |
       +-------------------+-------------------+
                           |
                 WokenLevelBootstrap
                           |
              Odalar, geçişler ve oyun akışı

    Planlanan LLM akışı: Unity Diyalog UI → Ollama → Gemma
```

### Temel Bileşenler

- **Oyuncu sistemi:** Hareketi ve çevreyle etkileşimi yönetti.
- **Yardımcı robot sistemi:** Oyuncu takibini, hedefe hareketi ve görev davranışlarını yönetti.
- **Bulmaca ve görev sistemleri:** Şifre paneli, batarya yuvası, kontrol düğmeleri ve kapı koşullarını yönetti.
- **Arayüz bileşenleri:** Nesne adlarını, bilgilendirme mesajlarını ve etkileşim pencerelerini gösterdi.
- **LLM diyalog bileşeni:** Oyuncu mesajlarının yerel modele iletilmesi ve yanıtların oyun içinde kullanılması için planlandı.

## 6. Oyun Mekanikleri

### Karakter Hareketi

Oyuncu WASD veya yön tuşlarıyla hareket etti. Robot ise oyuncuyu takip etti ve görev başladığında hedef noktaya yöneldi.

### Nesne Etkileşimleri

Laboratuvar nesnelerine isim ve açıklama etiketleri eklendi. Oyuncu nesnelere tıklayarak adlarını görebildi; etkileşim gerektiren noktalarda F tuşunu kullanabildi.

### Görev ve İlerleme Sistemi

Oda geçişleri ve bulmacalar belirli ilerleme koşullarına bağlandı. Şifre bulma, düğmeleri kullanma ve robotun görevleri tamamlaması oyun akışında ilerlemeyi sağladı.

### Elektrikli Kapı Mekanizması

Elektrikli kapının çalışması bataryanın yuvasına yerleştirilmesi koşuluna bağlandı. Batarya yerleştirilmeden kapı açılamadı; batarya takıldığında yuva göstergesi yeşile döndü.

## 7. Laboratuvar Haritası ve Bulmacalar

Laboratuvar, oyuncunun keşfedebileceği odalar ve katlar hâlinde düzenlendi. Odalardaki eşyalar, bulmacalar ve geçişler senaryonun ilerlemesine bağlandı.

### Bulmaca ve Keşif Öğeleri

- **Depo şifresi:** Laptop arşivindeki belgelerden edinilen ipuçlarıyla depo kapısının açılması sağlandı.
- **Laptop arşivi:** Deney raporları ve laboratuvar notları incelendi.
- **Gizli çekmece:** Kontrol panelindeki renkli düğmelerle açıldı; içindeki defter Gemma hakkında bilgi verdi.
- **Batarya ve elektrikli kapı:** Robotun yardımıyla batarya elektrikli kapının yuvasına taşındı.
- **Özel geçitler:** Robotun dar alanlardan geçerek bazı mekanizmaları çalıştırdığı görevler oluşturuldu.

## 8. Yardımcı Robot Sistemi

Yardımcı robot, oyuncuyu takip eden ve senaryodaki görevlerde otonom hareket eden bir karakter olarak geliştirildi. Robotun takip ve hedef hareketlerinde NavMesh tabanlı navigasyon kullanıldı.

Robotun temel davranışları şunlardı:

- Oyuncuyu takip etme.
- Belirlenen hedefe hareket etme.
- Görev sırasında takip davranışını geçici olarak durdurma.
- Görev tamamlandıktan sonra oyuncuyu yeniden takip etme.
- Oyuncuya oyun içi mesajlarla yönlendirme yapma.

```text
Oyuncu robotla ilerledi
        |
Bir görev koşulu oluştu
        |
Robot hedefe yöneldi
        |
NavMesh rotası takip edildi
        |
Görev tamamlandı
        |
Robot oyuncuyu takip etmeyi sürdürdü
```

## 9. Gemma ile Yerel LLM Diyaloğu

Bu bölüm, planlanan LLM entegrasyonunun tasarımını açıkladı. Unity ile Ollama arasında çalışan bir bağlantı henüz projeye eklenmedi; aşağıdaki iş akışı hedeflenen yapıyı gösterdi.

### Diyalog Sisteminin Hedefleri

- Oyuncunun Gemma’ya Türkçe mesaj göndermesi.
- Gemma’nın sabit cevaplar yerine mesajın bağlamına uygun yanıt üretmesi.
- Konuşma geçmişinin yanıt üretiminde kullanılması.
- Gemma’nın kişiliğini ve oyun senaryosunu koruması.
- Beş duygu durumunun konuşma biçimine yansıtılması.
- Görev veya kapı gibi oyun olaylarının Unity kurallarıyla doğrulanması.

### Duygu Durumları

| Duygu | Hedeflenen yaklaşım |
|---|---|
| Nötr | Dengeli ve bilgilendirici konuşma |
| Mutlu | Olumlu ve yardımsever yaklaşım |
| Öfkeli | Sert ve tepkisel konuşma |
| Üzgün | Çekingen ve duygusal yaklaşım |
| Korkmuş | Endişeli ve temkinli konuşma |

### Planlanan LLM Akışı

```text
Oyuncu mesajı
      |
Unity diyalog arayüzü
      |
Ollama yerel sunucusu
      |
Gemma modeli
      |
Yapılandırılmış yanıt
      |
Yanıt doğrulama
      |
NPC mesajı ve duygu durumu
      |
Gerekirse doğrulanmış oyun olayı
```

Örnek yanıt biçimi:

```json
{
  "response": "Sana neden güvenmem gerektiğini söyle.",
  "emotion": "korkmus",
  "action": "none"
}
```

Bu JSON yalnızca hedeflenen biçimi gösterdi; çalışan modelden alınmış veya doğrulanmış bir yanıt değildi. Kapı açma gibi oyun olaylarının son kararı Unity tarafındaki kurallar tarafından verilmesi planlandı.

## 10. Güncel Proje Durumu

| Sistem | Durum |
|---|---|
| Unity projesi ve sürüm kontrolü | Hazırlandı ve kullanıldı |
| Oyuncu hareketi | Geliştirildi |
| Laboratuvar alanları ve oda etiketleri | Geliştirildi |
| Nesne etkileşimleri ve bulmacalar | Geliştirildi |
| Yardımcı robot takibi | Geliştirildi |
| Robot navigasyonu ve görev davranışları | Geliştirildi |
| Elektrikli kapı ve batarya akışı | Geliştirildi |
| Gemma karakteri ve oyun senaryosu | Tasarlandı |
| Ollama–LLM bağlantısı | Henüz Unity’ye eklenmedi |
| Beş duygu durumlu dinamik diyalog | Entegrasyon bekliyor |
| Uçtan uca test ve son dokümantasyon | Sürdürülmesi planlandı |

## 11. Kurulum ve Çalıştırma

### Gereksinimler

- Windows.
- Unity Hub.
- Unity Editor **6000.6.5f1**.
- Git.
- Klavye ve fare.

### Depoyu Klonlama

```bash
git clone https://github.com/ilaydakrb/WOKEN.git
cd WOKEN
```

### Unity’de Açma

1. Unity Hub’ı açın.
2. **Add project from disk** seçeneğini seçin.
3. Klonlanan `WOKEN` klasörünü gösterin.
4. Projeyi Unity **6000.6.5f1** ile açın ve paketlerin yüklenmesini bekleyin.
5. `Assets/Scenes/SampleScene.unity` sahnesini açın.
6. **Play** düğmesine basarak oyunu başlatın.

### Temel Kontroller

| Girdi | İşlev |
|---|---|
| `W`, `A`, `S`, `D` veya yön tuşları | Oyuncuyu hareket ettirdi. |
| Fareyle sol tıklama | Etkileşimli nesnenin adını gösterdi. |
| `F` | Yakındaki nesne veya arayüzle etkileşim başlattı. |
| `T` | Taşınan malzemeyi robotta veya batarya yuvasında kullandı. |
| `Esc` | Açık arayüzü kapattı. |
| `Enter` veya `Space` | Defter kapağından sayfalara geçti. |
| `K`, `M`, `Y`, `S` | Kontrol paneline yaklaşıldığında kırmızı, mavi, yeşil ve sarı düğmeleri etkinleştirdi. Sarı düğme gizli çekmeceyi açtı. |
| Rakam tuşları | Depo şifre paneline kod girdi. |

LLM için Ollama ve model kurulum adımları entegrasyon tamamlandıktan sonra eklenecekti.

## 12. Testler

Oyun mekanikleri Unity Editor içinde farklı senaryolarda kontrol edildi. LLM bağlantısı henüz eklenmediğinden, LLM yanıt kalitesi ve bağlantı testleri tamamlanmadı.

| Test alanı | Kontrol edilen veya edilecek durum |
|---|---|
| Oyuncu hareketi | Yön tuşlarının ve çarpışmaların kontrolü |
| Nesne etkileşimi | Etiketlerin ve etkileşim mesajlarının kontrolü |
| Robot takibi | Takip davranışı ve oyuncuyla mesafenin kontrolü |
| Robot navigasyonu | Hedefe rota oluşturma ve ulaşma |
| Robot görevleri | Görev sırası ve görev sonrası takip davranışı |
| Bulmacalar | Şifre, kontrol düğmeleri ve batarya koşulları |
| Elektrikli kapı | Batarya takılmadan ve takıldıktan sonraki durum |
| LLM diyalogları | Entegrasyon sonrası bağlantı, JSON, duygu ve yanıt testleri |

LLM test senaryolarının; ikna girişimi, farklı duygu durumları, konu dışı mesajlar ve tekrarlanan istekleri kapsaması planlandı.

## 13. Karşılaşılan Sorunlar ve Çözümleri

Geliştirme sırasında Unity derleme hataları, NavMesh erişimi ve etkileşim akışları üzerinde düzenlemeler yapıldı. Robotun dar geçitlerdeki hareketi ve bazı oda geçişleri için özel hareket akışları eklendi. Şifre panelinde klavye ile giriş ve batarya takılmadan elektrikli kapının açılmaması gibi koşullar ayrıca ele alındı.

## 14. Ekran Görüntüleri

Aşağıdaki alanlar oyun görselleri eklendikçe güncellenecekti.

### Laboratuvar ve Oyun Ortamı

**Başlangıç Alanı**

![WOKEN Başlangıç Alanı](docs/screenshots/baslangic-alani.png)

**Birinci Kat ve Laboratuvar**

![WOKEN Birinci Kat](docs/screenshots/birinci-kat.png)

**İkinci Kat**

![WOKEN İkinci Kat](docs/screenshots/ikinci-kat.png)

**Depo ve Malzeme Odası**

![WOKEN Malzeme Odası](docs/screenshots/malzeme-odasi.png)

### Oyun Mekanikleri

**Oyuncu ve Yardımcı Robot**

![WOKEN Oyuncu ve Robot](docs/screenshots/oyuncu-robot.png)

**Robotun Görev Davranışı**

![WOKEN Robot Görevi](docs/screenshots/robot-gorevi.png)

**Nesne Etkileşimleri**

![WOKEN Etkileşim](docs/screenshots/nesne-etkilesimi.png)

**Elektrikli Kapı Mekanizması**

![WOKEN Elektrikli Kapı](docs/screenshots/elektrikli-kapi.png)

### Bulmacalar

**Renkli Düğmeler ve Gizli Çekmece**

![WOKEN Bulmaca](docs/screenshots/bulmaca.png)

### Yapay Zekâ Diyalogları

**Gemma NPC Diyalog Arayüzü**

![WOKEN NPC Diyalog](docs/screenshots/npc-diyalog.png)

**NPC Duygu Durumları**

![WOKEN NPC Duygular](docs/screenshots/npc-duygular.png)

*LLM diyalog görselleri entegrasyon tamamlandıktan sonra eklenecekti.*

## 15. Bilinen Eksikler

- Ollama üzerinden çalışan LLM bağlantısı Unity projesine henüz eklenmedi.
- Gemma’nın dinamik yanıtları ve beş duygu durumlu diyalog sistemi henüz uygulanmadı.
- LLM yanıt biçimi ve oyun olaylarıyla entegrasyon doğrulanmadı.
- LLM bağlantı kesintisi, zaman aşımı ve hatalı yanıt senaryoları test edilmedi.
- Ekran görüntüsü dosyaları README’de belirtilen klasöre henüz eklenmedi.
- Uçtan uca son testlerin ve teslim materyallerinin tamamlanması gerekti.

## 16. Proje Çıktıları

- Unity tabanlı 2D laboratuvar ortamı oluşturuldu.
- Oyuncu hareketi ve nesne etkileşim sistemleri geliştirildi.
- Birden fazla oda ve kat içeren oyun alanı hazırlandı.
- Bulmaca ve görev mekanikleri oluşturuldu.
- NavMesh tabanlı yardımcı robot hareketi geliştirildi.
- Robotun oyuncuyu takip etmesi ve belirli görevleri yerine getirmesi sağlandı.
- Batarya ve elektrikli kapı etkileşimi oyun akışına bağlandı.
- Yerel LLM ile Gemma diyalog sistemi için senaryo ve teknik plan hazırlandı.
- Ortak geliştirme ve sürüm kontrolü GitHub üzerinden yürütüldü.

## 17. Geliştiriciler

WOKEN iki kişilik bir ekip tarafından geliştirildi.

### Sueda

- Robot hareketi, navigasyon ve görev sistemleri.
- Oyun içi etkileşimler ve senaryo akışları.
- LLM entegrasyonu ve teknik dokümantasyon çalışmaları.

### İlayda

- Oyun arayüzü ve görsel düzenlemeler.
- Laboratuvar ortamı ve oyun bileşenlerinin geliştirilmesine katkı.

### Ortak Çalışmalar

- Oyun tasarımı ve genel planlama.
- Gemma karakterinin senaryosu ve LLM yaklaşımı.
- Sistem entegrasyonu, test ve dokümantasyon.

**GitHub:** [ilaydakrb/WOKEN](https://github.com/ilaydakrb/WOKEN)

## 18. Lisans ve Kullanım

Bu proje eğitim amacıyla geliştirildi. Projede kullanılan üçüncü taraf Unity paketleri ve diğer varlıklar kendi lisans koşullarına tabi oldu. Depoya ayrıca bir lisans dosyası eklenmediği için kaynak kodun yeniden kullanımı için açık bir lisans izni tanımlanmadı.

---

**WOKEN — Yapay zekâ destekli keşif, bulmaca ve kaçış deneyimi.**
