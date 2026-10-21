# WOKEN — Sistem Mimarisi

## 1. Genel Bakış

WOKEN, Unity ve C# kullanılarak geliştirilecek, terk edilmiş bir laboratuvarda geçen 2D keşif ve kaçış oyunudur. Sistem mimarisi; oyuncu kontrolü, oyun ortamı, yardımcı robot, otonom navigasyon ve yerel büyük dil modeli (LLM) entegrasyonu bileşenlerinden oluşacaktır.

Mimari tasarımın temel amacı, oyun bileşenlerini modüler biçimde geliştirmek ve aralarındaki iletişimi açık arayüzler üzerinden yönetmektir.

## 2. Temel Bileşenler

### 2.1. Oyuncu Sistemi

Oyuncunun oyun ortamında hareket etmesini, çevreyle etkileşim kurmasını ve bulmacaları çözmesini sağlar.

Temel sorumlulukları:

- Oyuncu hareketinin yönetilmesi
- Oyun nesneleriyle etkileşimin sağlanması
- Oyuncu girdilerinin işlenmesi
- Oyun mekanikleriyle iletişim kurulması

### 2.2. Harita ve Oyun Ortamı

Laboratuvarın fiziksel yapısını, hareket alanlarını, engelleri ve etkileşim noktalarını içerir.

Temel sorumlulukları:

- Oyun alanlarının ve geçişlerin tanımlanması
- Çarpışma ve hareket sınırlarının belirlenmesi
- Robot ve oyuncunun hareket edebileceği alanların oluşturulması
- Navigasyon sisteminin ihtiyaç duyduğu çevresel bilgilerin sağlanması

### 2.3. Yardımcı Robot

Oyuncuya eşlik eden robotun oyun içindeki davranışlarını yönetir. Robotun çevresini algılaması, hedeflere yönelmesi ve oyuncuyla etkileşim kurması hedeflenmektedir.

Temel sorumlulukları:

- Çevresel algılama sonuçlarının değerlendirilmesi
- Hedef belirleme ve navigasyon sisteminin kullanılması
- Hareket davranışlarının yönetilmesi
- Oyuncuyla etkileşim kurulması
- Gerekli durumlarda LLM bileşeninden gelen yanıtların kullanılması

### 2.4. Navigasyon ve Çevre Algılama

Robotun oyun ortamında hareket edebilmesi için gereken algılama ve yön bulma işlevlerini kapsar.

Temel sorumlulukları:

- Robotun çevresindeki engellerin algılanması
- Hedef ve hareket alanlarının değerlendirilmesi
- Navigasyon sisteminin uygun hareket rotası belirlemesine yardımcı olunması
- Hareketin başarısız olduğu veya hedefe ulaşılamadığı durumların ele alınması

Navigasyon yöntemi, Unity sürümü ve 2D ortamın teknik gereksinimleri değerlendirilerek belirlenecektir.

### 2.5. Yerel LLM Bileşeni

Robotun oyuncuyla daha esnek ve bağlama uygun diyaloglar kurabilmesi için yerel bir büyük dil modelinden yararlanılması planlanmaktadır. Modelin Ollama aracılığıyla çalıştırılması hedeflenmektedir.

Temel sorumlulukları:

- Oyun tarafından oluşturulan istemlerin modele iletilmesi
- Model yanıtlarının alınması ve işlenmesi
- Yanıtların beklenen biçime uygunluğunun kontrol edilmesi
- Hata ve zaman aşımı durumlarının yönetilmesi
- Robotun diyalog sistemiyle iletişim kurulması

LLM, temel oyuncu hareketi veya robot navigasyonunun yerine geçmeyecek; bu sistemleri tamamlayan bir bileşen olarak kullanılacaktır.

## 3. Bileşenler Arası İletişim

Sistemlerin aşağıdaki akış doğrultusunda birlikte çalışması hedeflenmektedir:

1. Oyuncu, oyun ortamında hareket eder veya bir etkileşim başlatır.
2. Oyun sistemleri, oyuncunun durumunu ve ilgili çevresel bilgileri değerlendirir.
3. Robotun hareket veya etkileşim ihtiyacı belirlenir.
4. Navigasyon bileşeni, robotun hedefe ulaşmasına yardımcı olur.
5. Diyalog gerektiğinde oyun, LLM bileşenine uygun bir istek gönderir.
6. LLM yanıtı alınır, doğrulanır ve robotun diyalog sisteminde kullanılır.
7. Oyun, oluşan durumu kullanıcıya yansıtır.

Bu akış hedeflenen mimariyi temsil eder. Uygulama sırasında bileşenlerin sorumlulukları ve veri alışverişi netleştirilecektir.

## 4. LLM İletişim Yapısı

Yerel modelle iletişim kuran bileşenin oyun mantığından ayrılması planlanmaktadır. Böylece model değişikliği veya iletişim hataları diğer oyun sistemlerini mümkün olduğunca az etkileyecektir.

İletişim tasarımında aşağıdaki ilkeler gözetilecektir:

- Model istekleri ana oyun akışını gereksiz yere bloke etmemelidir.
- İstekler için zaman aşımı ve hata yönetimi bulunmalıdır.
- Model yanıtları kullanılmadan önce doğrulanmalıdır.
- Modelin ürettiği metin, doğrudan güvenilir bir oyun komutu olarak kabul edilmemelidir.
- Oyun durumunu değiştiren eylemler oyun kodu tarafından kontrol edilmelidir.

## 5. Modülerlik ve Bağımlılıklar

Bileşenler, sorumlulukları birbirinden ayrılacak şekilde tasarlanacaktır.

- Oyuncu sistemi, robotun iç uygulama ayrıntılarına bağımlı olmamalıdır.
- Robot hareketi, LLM yanıtının gelmesini beklemek zorunda olmamalıdır.
- Navigasyon bileşeni, harita ve çarpışma verileriyle uyumlu çalışmalıdır.
- LLM iletişimi, robotun diyalog ve etkileşim katmanından ayrılmalıdır.
- Sistemler arası bağlantılar mümkün olduğunca açık ve test edilebilir arayüzler üzerinden kurulmalıdır.

## 6. Hata Yönetimi

Sistemin kararlılığını artırmak için aşağıdaki durumlar ele alınacaktır:

- Robotun hedefe ulaşamaması
- Navigasyon veya çevre algılama hataları
- Yerel modelin çalışmaması
- İsteklerin zaman aşımına uğraması
- Boş, hatalı veya beklenmeyen model yanıtları
- Oyun bileşenleri arasında uyumsuz veri aktarımı

Hata durumlarında oyun akışının devam edebilmesi ve kullanıcıya uygun geri bildirim verilmesi hedeflenmektedir.

## 7. Mimari Durum

Bu belge, projenin hedeflenen sistem mimarisini tanımlar. Bileşenler uygulandıkça, entegrasyon kararları kesinleştikçe ve test sonuçları elde edildikçe güncellenecektir.
