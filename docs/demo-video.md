# Demo videosu çekim senaryosu (yaklaşık 2 dakika)

Amaç: İşe alım sorumlusu videoyu sessiz izlese bile projenin ne olduğunu ve neden etkileyici olduğunu anlasın.
Her sahnenin başına ekrana kısa bir yazı koyun (ör. "1 · Misafir rezervasyon yapıyor").

## Hazırlık

1. `.env` içinde `DEMO_MODE=true` yapın ve `docker compose up --build` çalıştırın.
2. İki pencere hazırlayın:
   - **Sol:** Tarayıcıda `http://localhost:8080`, "Demo hesabıyla dene" ile girilmiş, **Takvim** sayfası açık.
   - **Sağ:** Misafir uygulaması. Telefonda Expo Go ile ya da tarayıcıda (`npx expo start` → `w`), geliştirici araçlarında telefon görünümüyle.
3. Kayıt aracı: Windows'ta `Win + Alt + R` (Xbox Game Bar) ya da OBS. Çözünürlük 1920×1080.

## Sahneler

| Süre | Ekran | Ne gösterilir | Ekran yazısı |
|---|---|---|---|
| 0:00–0:10 | Panel, genel bakış | Doluluk kartları ve 7 gecelik grafik | "StayFlow: butik oteller için rezervasyon sistemi" |
| 0:10–0:35 | Misafir uygulaması | Keşfet → Lara Butik Otel → 2 gece seç → Standart "Seç" → ad ve e-posta → "Rezervasyon yap" | "1 · Misafir rezervasyon yapıyor" |
| 0:35–0:45 | **İki pencere yan yana** | Rezervasyon oluştuğu anda panelde "Yeni rezervasyon" bildirimi çıkar, takvimde blok belirir | "2 · Otel paneli anında güncellenir (SignalR)" |
| 0:45–1:00 | Misafir uygulaması | 15 dk sayacı → önce `4000 0000 0000 0002` (reddedilir), sonra `4242 4242 4242 4242` → "Onaylandı" ve rezervasyon kodu | "3 · Ödeme: 15 dakika içinde yapılmazsa oda otomatik satışa açılır" |
| 1:00–1:15 | Panel, takvim | Rezervasyona tıkla → Onaylı → "Check-in yap" | "4 · Resepsiyon check-in yapar" |
| 1:15–1:35 | Panel, yorumlar | Puan dağılımı → "Yorumları özetle" → bir yoruma "Yapay zekâ ile öner" | "5 · Yapay zekâ yorumları özetler ve yanıt taslağı önerir" |
| 1:35–1:55 | Terminal | `python scripts/smoke_test.py` çıktısının son satırları: 20 eşzamanlı istek → 1 × 201, 19 × 409 | "6 · Aynı oda aynı anda iki kez satılamaz (veritabanı kısıtı)" |
| 1:55–2:00 | README | Mimari diyagramı | GitHub adresi |

> Yapay zekâ sahnesi için `.env` içinde gerçek bir `ANTHROPIC_API_KEY` gerekir. Yoksa bu sahneyi atlayın.

## LinkedIn paylaşımı için kısa metin (taslak)

> Son birkaç haftada StayFlow'u geliştirdim: butik oteller için çok kiracılı bir rezervasyon sistemi.
> .NET 10 API, React yönetim paneli ve React Native misafir uygulamasından oluşuyor.
> En sevdiğim problem çifte rezervasyondu: aynı oda için 30 eşzamanlı istekten yalnızca birinin kazanmasını
> veritabanı kısıtıyla garanti ettim ve bunu her CI çalışmasında gerçek SQL Server üzerinde test ediyorum.
> Hangfire, SignalR, Redis, Docker, GitHub Actions ve Claude API ile yorum özetleme de işin içinde.
> Kod ve demo: https://github.com/hlygns/StayFlow
