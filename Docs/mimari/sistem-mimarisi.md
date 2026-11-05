# WOKEN — Sistem Mimarisi

## 1. Genel Bakış

WOKEN, Unity ve C# ile geliştirilen, terk edilmiş bir laboratuvarda geçen 2D keşif ve kaçış oyunudur. Oyuncu laboratuvarı keşfeder, çevredeki nesnelerle etkileşime girer ve çıkışa ulaşmak için robot yardımcısıyla birlikte görevleri tamamlar.

Sistem; oyuncu kontrolü, oyun dünyası, robot hareketi ve görevleri, etkileşim arayüzleri ve navigasyon bileşenlerinden oluşur. Bu bileşenlerin önemli bir bölümü `WokenLevelBootstrap` tarafından oyun başında oluşturulur ve birbirine bağlanır.

## 2. Temel Bileşenler

### 2.1. Oyun Dünyası ve Başlatma

`WokenLevelBootstrap`, oyun alanlarını, odaları, nesneleri ve karakterleri hazırlar. Duvarlar ve çevre nesneleri çarpışma bileşenleriyle oluşturulur; karakterlerin hareket edeceği alan için 2D NavMesh hazırlanır.

Sınıf ayrıca oyuncu, robot, etkileşim arayüzleri, asansör geçişi ve oda durumları arasındaki bağlantıları kurar. Böylece sahnedeki temel oyun akışı tek bir başlangıç noktasından başlatılır.

### 2.2. Oyuncu Sistemi

`PlayerWASDController`, oyuncunun WASD ve yön tuşlarıyla hareketini yönetir. Hareket, `Rigidbody2D` üzerinden uygulanır; karakterin animasyonu ise hareket yönüne göre güncellenir.

Oyuncu; nesne, kapı, parola paneli, laptop ve diğer etkileşim noktalarıyla ilgili işlemleri klavye girdileri ve ilgili etkileşim bileşenleri üzerinden gerçekleştirir.

### 2.3. Yardımcı Robot ve Görevler

`RobotMovementController`, robotun oyuncuyu takip etmesini ve belirlenmiş hedeflere gitmesini yönetir. Robotun hareket rotası `NavMeshAgent` tarafından hesaplanır; fiziksel konumu ise `Rigidbody2D` ile eşgüdümlü biçimde güncellenir.

Robot iki temel hareket durumunda çalışır:

- Oyuncuyu takip etme
- Bir görev hedef noktasına gitme

Görev tamamlandığında, iptal edildiğinde veya hedefe ulaşmak için rota bulunamadığında sonuç bilgisi diğer bileşenlere iletilir. `RobotRouteGuideUI`, robotun konuşmalarını, görev akışını ve oyuncuya verdiği yönlendirmeleri yönetir.

### 2.4. Navigasyon ve Rota Planlama

Robotun oyuncuyu takip etmesi ve görev hedeflerine gitmesi için NavMesh kullanılır. NavMesh, odalardaki yürünebilir alanları ve engelleri dikkate alarak robotun hareket rotasını belirler.

Projede ayrıca `GridRoutePlanner` adında, ızgara tabanlı rota seçenekleri üretebilen bir yardımcı sınıf bulunur. Bu sınıf NavMesh ile aynı işi yapmaz; robotun mevcut takip ve görev hareketinde kullanılan ana navigasyon sistemi NavMesh’tir.

### 2.5. Etkileşim ve Arayüz Bileşenleri

Etkileşimler, ilgili görev veya nesne türüne göre ayrı bileşenlerde yönetilir. Örneğin:

- `WorldObjectNameUI`, tıklanan nesnelerin adını ve kısa bilgilendirme mesajlarını gösterir.
- `DepotKeypadUI`, depo kapısının şifre girişini ve kapı durumunu yönetir.
- `BatteryBayInteractionUI`, bataryanın alınması ve asansör yuvasına takılması akışını yürütür.
- `LaptopArchiveUI`, laptop üzerindeki dosyaları ve oyun içi ipuçlarını gösterir.
- `HiddenDrawerNotebookUI`, gizli çekmecedeki defterin açılmasını ve içeriğinin okunmasını sağlar.

Bu yapı, her etkileşimin kendi sorumluluğunda tutulmasına yardımcı olur.

### 2.6. Keşif ve Görüş Alanı

`WorldExplorationFog`, haritanın keşfedilmemiş alanlarını örter. Oyuncu hareket ettikçe yakınındaki ve engellerin arkasında kalmayan hücreler açılır. Keşfedilen alanlar açık kalır.

### 2.7. LLM Entegrasyonu

Oyunda Gemma adlı bir yapay zekâ karakteri ve onunla ilgili anlatı öğeleri bulunur. Ancak mevcut kaynak kodunda oyuncu mesajlarını bir büyük dil modeline gönderip yanıt alan bir LLM bağlantısı henüz yer almamaktadır.

LLM entegrasyonu eklendiğinde, modelle iletişim oyun hareketi ve görev mantığından ayrı bir bileşende tutulmalıdır. Modelden gelen yanıtlar metin olarak değerlendirilmeli; oyun dünyasını değiştiren hareketler ve görevler oyun kodu tarafından doğrulanıp uygulanmalıdır.

> **Güncelleme notu:** Gerçek LLM bağlantısı tamamlandıktan sonra bu bölüm; kullanılan model ve iletişim yöntemi, duygu durumlarına göre yanıt üretimi, hata yönetimi ve oyun sistemleriyle veri akışı açıklanacak şekilde güncellenmelidir. Son proje kontrolünde sistem mimarisinin ilgili bölümleri de yeniden gözden geçirilmelidir.

## 3. Bileşenler Arası Akış

1. `WokenLevelBootstrap`, oyun alanını ve sistem bileşenlerini oluşturur.
2. Oyuncu, `PlayerWASDController` aracılığıyla dünyada hareket eder.
3. Etkileşim bileşenleri oyuncunun bulunduğu konuma ve girdilerine göre ilgili işlemi başlatır.
4. Robotun takip veya görev davranışını `RobotMovementController` yürütür.
5. NavMesh, robotun yürünebilir alanlardaki rotasını hesaplar.
6. `RobotRouteGuideUI` görev durumunu ve robotun mesajlarını gösterir.
7. Oyun dünyasındaki değişiklikler ilgili bileşenler tarafından uygulanır; örneğin batarya takılınca yuva ışığı yeşile döner ve asansör etkinleşir.

## 4. Hata Durumları

Sistemlerde hedefe ulaşılamaması, rota bulunamaması, eksik nesne referansları veya etkileşim için gerekli koşulların sağlanmaması gibi durumlar ele alınır. Robotun görev sonucu ilgili sisteme iletilir ve oyuncuya uygun bir mesaj gösterilebilir.

LLM entegrasyonu eklendiğinde ayrıca bağlantı hatası, zaman aşımı ve boş ya da beklenmeyen yanıtlar için hata yönetimi uygulanmalıdır.

## 5. Mimari İlkeler

- Her bileşen belirli bir sorumluluğu yönetir.
- Robot hareketi ile konuşma ve arayüz akışı birbirinden ayrılır.
- Nesne etkileşimleri ilgili etkileşim bileşenlerinde tutulur.
- Oyun durumunu değiştiren kararlar oyun kodunun denetiminde kalır.
- Yeni özellikler mevcut sistemlere mümkün olduğunca sınırlı bağımlılıkla eklenir.
