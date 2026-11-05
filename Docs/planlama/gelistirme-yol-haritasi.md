# WOKEN — Geliştirme Yol Haritası

## 1. Planlama ve Proje Kurulumu

- Oyunun temel fikri, hedefi ve ana senaryosu belirlendi.
- Unity projesi ve GitHub üzerinden sürüm kontrolü oluşturuldu.
- Ekip üyelerinin sorumlulukları belirlendi.
- Proje dokümantasyonu için klasör yapısı hazırlandı.

## 2. Temel Oynanış Sistemleri

- Oyuncunun WASD ve yön tuşlarıyla hareket etmesi sağlandı.
- Oyuncu ve robot karakterleri oyun alanına eklendi.
- Nesne adları ve kısa bilgilendirme mesajları için etkileşim sistemi geliştirildi.
- Oyun içi mesaj baloncukları ve etkileşim arayüzleri oluşturuldu.

## 3. Laboratuvar Haritası ve Bulmacalar

- Laboratuvar odaları, geçişler ve çevre nesneleri oluşturuldu.
- Odalardaki nesnelere etiketler eklendi.
- Depo şifresi, laptop arşivi ve gizli çekmece defteri gibi keşif öğeleri hazırlandı.
- Renkli düğmeler ve gizli çekmece bulmacası oluşturuldu.
- Elektrikli kapının açılması, bataryanın yuvaya takılması koşuluna bağlandı.

## 4. Yardımcı Robot ve Görev Akışları

- Robotun oyuncuyu takip etmesi sağlandı.
- Robotun hedef noktalarına gitmesi için NavMesh tabanlı hareket eklendi.
- Robotun dar geçit, buton ve basamak gibi senaryolardaki görev davranışları geliştirildi.
- Bataryanın alınması ve elektrikli kapının batarya yuvasına taşınması robotun görev akışına bağlandı.
- Robotun oyuncuya senaryo boyunca mesajlarla yönlendirme yapması sağlandı.

## 5. Oyun Akışının ve Etkileşimlerin Birleştirilmesi

- Odalar arasındaki ilerleme sırası ve geçiş koşulları oluşturuldu.
- Oyuncu, robot, bulmacalar ve çevre etkileşimleri bir araya getirildi.
- Tamamlanamayan görevler için oyuncuya geri bildirim eklendi.
- Robotun görevi olmadığında oyuncuyu takip etmesi sağlandı.

## 6. Gemma ile LLM Diyaloğu

- Oyun ile yerel Gemma dil modeli arasında iletişim kurulması planlandı.
- Oyuncunun Gemma'ya mesaj gönderebilmesi için gerekli yapı tasarlandı.
- Gemma'nın yanıtlarının oyun bağlamı ve senaryoyla uyumlu olması hedeflendi.
- Beş farklı duygu durumuna göre yanıt tarzları planlandı.
- İstek, yanıt, bağlantı hatası ve zaman aşımı durumlarının yönetilmesi kararlaştırıldı.
- LLM yanıtlarının oyun içindeki görevleri kontrolsüz biçimde değiştirmesini engelleyecek kurallar belirlendi.

## 7. Test ve İyileştirme

- Oyunun baştan sona oynanarak senaryo akışının kontrol edilmesi planlandı.
- Robotun takibi, navigasyonu ve görevlerinin farklı durumlarda test edilmesi kararlaştırıldı.
- Nesne etkileşimleri ve bulmaca çözümlerinin doğrulanması planlandı.
- Gemma ile farklı duygu durumlarında diyalog senaryolarının test edilmesi kararlaştırıldı.
- Hataların, anlaşılmayan yanıtların ve bağlantı kesintilerinin incelenmesi planlandı.
- Görsel okunabilirlik, mesaj süreleri ve geçiş hızlarının son kez gözden geçirilmesi kararlaştırıldı.

## 8. Dokümantasyon ve Teslim

- Sistem mimarisinin LLM bağlantısı tamamlandıktan sonra güncellenmesi planlandı.
- README ve gerekli kurulum/çalıştırma yönergelerinin hazırlanması kararlaştırıldı.
- Test sonuçlarının ve bilinen sınırlamaların dokümante edilmesi planlandı.
- Projenin son sürümünün GitHub üzerinde düzenlenmesi kararlaştırıldı.
- Sunum ve teslim materyallerinin hazırlanması planlandı.

## Geliştirme İlkesi

Yol haritası, tamamlanan işleri ve sıradaki geliştirme adımlarını gösterecek şekilde hazırlandı. Yeni kararlar alındıkça veya testlerde ek çalışma gerektiği görüldükçe güncellenmesi kararlaştırıldı.