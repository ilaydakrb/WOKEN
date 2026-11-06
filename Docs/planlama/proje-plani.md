# WOKEN — Proje Planı

## 1. Projenin Amacı

WOKEN, Unity ve C# kullanılarak geliştirilen, yapay zekâ destekli 2D bulmaca ve kaçış oyunudur. Oyuncunun terk edilmiş bir laboratuvarda çevresini keşfederek çeşitli görevleri tamamlaması, bulmacaları çözmesi ve çıkış yolunu bulması amaçlandı.

Projede, oyuncuya yardımcı olan otonom bir robot ve yerel dil modeliyle çalışan bir NPC olmak üzere iki farklı yapay zekâ sistemi kullanılmasına karar verildi.

## 2. Projenin Kapsamı

Proje kapsamında laboratuvar ortamı, oyuncu hareket sistemi, nesne etkileşimleri, bulmacalar, yardımcı robot davranışları ve yapay zekâ destekli NPC diyalogları ele alındı.

Oyuncunun odalar arasında ilerleyebilmesi için belirli görevleri tamamlaması ve çevredeki nesnelerle etkileşime geçmesi planlandı. Yardımcı robotun oyuncuyu takip etmesi, belirlenen hedeflere hareket etmesi ve görevlerde destek sağlaması amaçlandı.

Ayrıca oyuncunun NPC ile Türkçe yazılı iletişim kurabildiği, NPC'nin konuşmanın içeriğine göre farklı duygu durumları sergilediği ve dinamik yanıtlar ürettiği bir sistem tasarlanması kararlaştırıldı.

## 3. Kullanılan Teknolojiler

- **Unity:** Oyun ortamının, sahnelerin ve oynanış mekaniklerinin geliştirilmesi için seçildi.
- **C#:** Oyuncu hareketleri, robot davranışları ve oyun içi etkileşimlerin programlanması için kullanıldı.
- **NavMeshPlus ve AI Navigation:** Yardımcı robotun rota oluşturması ve hedef noktalara hareket etmesi için kullanıldı.
- **Ollama ve Gemma:** Tam etiket `gemma3-lab-text:latest`, kaynak `gemma3:4b`; yaklaşık 3,9B parametreli Q4_K_M metin profili yerel çalıştırılıyor.
- **Python:** Yerel oyun API'si, değerlendirme ve testler için kullanılıyor. Çalışma paketi LLMService içindedir; üçüncü taraf pip paketi gerekmez.
- **Git ve GitHub:** Sürüm kontrolü ve ekip içi kod paylaşımı için kullanıldı.

## 4. Geliştirme Aşamaları

### Planlama ve Hazırlık

Oyunun temel senaryosu, görev yapısı ve kullanılacak teknolojiler belirlendi. Unity projesi oluşturuldu ve GitHub üzerinden ortak geliştirme ortamı hazırlandı.

### Oyun Ortamı ve Temel Mekanikler

Laboratuvarın odaları ve çevre nesneleri tasarlandı. Oyuncu hareketleri, nesne etkileşimleri, bilgilendirme mesajları ve temel bulmaca mekanikleri geliştirildi.

### Yardımcı Robot Sistemi

Robotun oyuncuyu takip etmesi ve hedef noktalara hareket etmesi için NavMesh tabanlı navigasyon sistemi oluşturuldu. Robotun görev davranışları ve oyun senaryosuyla etkileşimleri geliştirildi.

### Yerel LLM Sistemi

NPC diyaloglarında Ollama üzerinde `gemma3-lab-text:latest` kullanılıyor; kaynak `gemma3:4b` modelidir. Ağırlık eğitimi/fine-tuning yapılmadı; hazır model sistem talimatları, her tura özel bağlam ve Python karar kurallarıyla yönlendiriliyor. İlk dört tur, beşinci tur ve olgu çıkarımı için kullanılan gerçek sistem metinleri [sistem mimarisinde](../mimari/sistem-mimarisi.md#gerçek-model-bilgisi-ve-system-prompt) ve README'nin 9. bölümünde yer alır.

### Donanıma göre model tercihi

Yerel doğrulamada i7-10750H, 8 GB RAM ve GTX 1650 Ti / 4 GiB VRAM kullanıldığı görüldü. `gemma3-lab-text:latest` yaklaşık 3,9B Q4_K_M metin profilidir; canlı Ollama kaydında dosya boyutu yaklaşık 2,50 GB'dir. Windows ve Unity'ye alan bırakmak için düşük bitli ağırlık temsili ve 2048 token bağlam kullanılıyor. Dosya boyutu tepe RAM/VRAM tüketimi olarak sunulmuyor. Tarihsel tam model testinde 40 mesajın ortalaması 18,40 sn, ilk Unity bağlantısında tek yanıt 105,45 sn idi; hızlı yanıt garantisi yoktur. Ayrıntılı donanım, bellek sınırları ve ölçüm kapsamı sistem mimarisi belgesindeki ilgili bölümde açıklanmıştır. Eski üç donanım fotoğrafı kaldırılmıştır.

### Entegrasyon ve Test

Yerel LLM sisteminin bağımsız testlerden sonra Unity oyununa bağlanması planlandı. Oyun mekanikleri, robot davranışları, NPC diyalogları ve hata durumlarının farklı senaryolarla değerlendirilmesi kararlaştırıldı.

## 5. Görev Dağılımı ve Ekip Çalışması

Proje, Sueda ve İlayda tarafından ortak geliştirildi. Sueda, yardımcı robotun navigasyon ve görev sistemleri ile oyun içi etkileşimler üzerinde çalıştı. İlayda, laboratuvar ortamının tasarımı, animasyonlar ve geçiş mekanikleri üzerinde çalıştı.

Oyunun genel planlaması, yerel LLM sistemi, test süreçleri ve dokümantasyon çalışmaları ortak sorumluluk olarak belirlendi. Geliştirme sürecinde GitHub kullanılarak yapılan değişikliklerin takip edilmesi ve proje dosyalarının güncel tutulması sağlandı.

## 6. Test ve Başarı Kriterleri

Projenin değerlendirilmesi için aşağıdaki kriterler belirlendi:

- Oyuncu hareketlerinin ve nesne etkileşimlerinin doğru çalışması.
- Robotun oyuncuyu takip etmesi ve görevlerini uygun şekilde tamamlaması.
- Bulmacaların ve elektrikli kapı geçiş koşullarının doğru çalışması.
- NPC'nin Türkçe, bağlama uygun ve dinamik yanıtlar üretmesi.
- Beş farklı duygu durumunun diyaloglara doğru yansıtılması.
- Bağlantı hatalarının ve zaman aşımı durumlarının oyun akışını bozmadan yönetilmesi.

## 7. Beklenen Sonuç

Proje sonunda oyuncunun laboratuvar ortamında keşif yapabildiği, bulmacaları çözebildiği, yardımcı robotla görevleri tamamlayabildiği ve yerel LLM destekli NPC ile dinamik iletişim kurabildiği bir oyun ortaya çıkarılması hedeflendi.

Geliştirilen sistemlerin tek bir oyun içerisinde birleştirilmesiyle yapay zekâ destekli etkileşimlerin oyun deneyimine katkısının gösterilmesi amaçlandı.