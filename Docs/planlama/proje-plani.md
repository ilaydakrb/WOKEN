# WOKEN — Proje Planı

## 1. Proje Tanımı

WOKEN, terk edilmiş bir laboratuvarda geçen, keşif, bulmaca çözme ve kaçış mekaniklerine dayalı 2D bir oyun projesidir. Oyuncunun temel amacı, laboratuvar ortamını keşfederek ilerlemek ve karşılaştığı engelleri çözerek kaçış yolunu bulmaktır.

Oyunun önemli bileşenlerinden biri, oyuncuya eşlik eden yardımcı robot karakteridir. Robotun çevresini algılayabilmesi, otonom hareket edebilmesi ve oyuncuyla etkileşim kurabilmesi hedeflenmektedir.

Projede ayrıca yerel bir büyük dil modeli (LLM) kullanılarak robotun oyuncuyla daha esnek ve bağlama uygun diyaloglar kurması amaçlanmaktadır.

## 2. Projenin Amaçları

- Unity ve C# kullanarak oynanabilir bir 2D oyun prototipi geliştirmek.
- Keşif, etkileşim ve bulmaca çözme mekaniklerini tasarlamak.
- Yardımcı robot için çevre algılama ve otonom navigasyon sistemi geliştirmek.
- Yerel LLM entegrasyonunu oyun mimarisine dahil etmek.
- Yapay zekâ ile oyun sistemleri arasındaki iletişimi yapılandırılmış verilerle yönetmek.
- Yazılım geliştirme, sürüm kontrolü, test ve teknik dokümantasyon süreçlerini uygulamak.

## 3. Proje Kapsamı

### 3.1. Oyun Mekanikleri

- Oyuncu hareketi ve temel kontroller
- Laboratuvar ortamının keşfedilmesi
- Nesnelerle etkileşim
- Bulmaca ve ilerleme mekanikleri
- Oyun içi arayüzler

### 3.2. Yardımcı Robot

- Robot karakterinin oyun ortamında bulunması
- Çevre algılama mekanizmaları
- Navigasyon ve hedefe yönelme
- Oyuncuyla etkileşim
- Hata durumlarının ve ulaşılamayan hedeflerin yönetimi

### 3.3. Yapay Zekâ Entegrasyonu

- Ollama üzerinden yerel dil modeli çalıştırılması
- Oyun ile dil modeli arasındaki iletişimin kurulması
- İstek ve yanıtların yapılandırılmış biçimde işlenmesi
- Asenkron iletişim ve hata yönetimi
- Model yanıtlarının oyun sistemleri açısından doğrulanması

## 4. Teknik Yaklaşım

Oyun Unity ve C# kullanılarak geliştirilecektir. Robotun hareketi, çevre algılama ve navigasyon bileşenleri birbirinden ayrılabilir şekilde tasarlanacaktır.

Yerel dil modeli, oyun mantığından bağımsız bir iletişim bileşeni üzerinden kullanılacaktır. Modelin ürettiği yanıtlar doğrudan ve kontrolsüz biçimde oyun durumunu değiştirmeyecek; gerekli doğrulama ve izin kontrollerinden geçirilecektir.

## 5. Geliştirme İlkeleri

- Modüler ve sürdürülebilir kod yapısı
- Ekip üyeleri arasında görevlerin açık biçimde paylaşılması
- Git ve GitHub ile sürüm kontrolü
- Teknik kararların dokümante edilmesi
- Gerçekleştirilen çalışmaların düzenli olarak raporlanması
- Test edilebilir bileşenlerin geliştirilmesi
- Tamamlanmamış özelliklerin dokümantasyonda açıkça belirtilmesi

## 6. Başarı Ölçütleri

- Oyuncunun temel oyun mekaniklerini kullanabilmesi
- Robotun belirlenen hedeflere güvenilir biçimde yönelmesi
- Çevre algılama sonuçlarının robot davranışlarında kullanılabilmesi
- Yerel LLM ile oyun arasında kararlı iletişim kurulması
- Hatalı veya beklenmeyen model yanıtlarının güvenli biçimde yönetilmesi
- Projenin kurulum, mimari, test ve geliştirme süreçlerinin dokümante edilmesi

## 7. Proje Durumu

Proje başlangıç ve yapılandırma aşamasındadır. Yukarıda belirtilen özellikler hedeflenen kapsamı tanımlar; tamamlanma durumları ara raporlarda ve test sonuçlarında güncellenecektir.
