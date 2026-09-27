# StayFlow

Butik oteller ve pansiyonlar için çok kiracılı (multi-tenant) rezervasyon ve misafir yönetim sistemi.
Üç parçadan oluşur:

- **Otel paneli** (React): Oteller kaydolur; odalarını, fiyatlarını, rezervasyonlarını ve yorumlarını yönetir.
- **Misafir uygulaması** (React Native): Misafir otel arar, rezervasyon yapar, öder ve yorum yazar.
- **API** (.NET 10): Her otel yalnızca kendi verisini görür.

![Rezervasyon takvimi](docs/screenshots/calendar.png)

| Misafir uygulaması: keşfet | Otel ve müsaitlik | Ödeme (15 dk süre) | Onaylandı |
|---|---|---|---|
| ![Keşfet](docs/screenshots/mobile-explore.png) | ![Otel](docs/screenshots/mobile-hotel.png) | ![Ödeme](docs/screenshots/mobile-payment.png) | ![Onay](docs/screenshots/mobile-confirmed.png) |

## Mimari

```mermaid
flowchart LR
    subgraph clients[İstemciler]
        panel["Otel paneli<br/>React + TypeScript"]
        mobile["Misafir uygulaması<br/>React Native (Expo)"]
    end
    nginx["Nginx<br/>HTTPS"]
    api["ASP.NET Core API<br/>.NET 10"]
    db[("SQL Server")]
    redis[("Redis<br/>müsaitlik önbelleği")]
    jobs["Hangfire<br/>zamanlanmış işler"]
    hub["SignalR hub<br/>otel başına grup"]
    push["Expo Push"]
    ai["Claude API<br/>yorum özeti"]
    smtp["SMTP<br/>e-posta"]

    panel -- "REST + WebSocket" --> nginx
    mobile -- "REST /api/public" --> nginx
    nginx --> api
    api --> db
    api --> redis
    api --- jobs
    api --- hub
    hub -. "canlı güncelleme" .-> panel
    api --> push -. "bildirim" .-> mobile
    api --> ai
    jobs --> smtp
```

```
backend/
├── StayFlow.Domain          → Entity'ler (Hotel, Room, Reservation, RoomNight, Review...) - bağımlılığı yok
├── StayFlow.Application     → İş kuralları, servisler, DTO'lar, arka plan işlerinin mantığı, arayüzler
├── StayFlow.Infrastructure  → EF Core + migration'lar, e-posta, Claude, Expo push, demo verisi
├── StayFlow.API             → Controller'lar, JWT, SignalR hub, Hangfire, loglama, deneme sınırları
└── StayFlow.Tests           → xUnit: servis, HTTP entegrasyon ve Testcontainers testleri
admin-panel/                 → React 19 + Vite + Tailwind + TanStack Query
mobile/                      → Expo SDK 57 + Expo Router (iOS, Android, web)
scripts/smoke_test.py        → Çalışan sisteme karşı uçtan uca test (CI'da da koşar)
```

Bağımlılıklar içe doğru akar. Örneğin Application katmanı SignalR'ı, Claude'u ya da Expo'yu bilmez;
yalnızca `IReservationNotifier` ve `IReviewAssistant` gibi arayüzleri bilir.

## Öne çıkan mühendislik problemleri

### 1. Çifte rezervasyon
İki misafir aynı anda aynı odayı aynı tarihler için ayırtmaya çalışırsa ne olur? "Önce kontrol et, sonra kaydet" yaklaşımı yetmez:
iki istek de kontrolü aynı anda geçip ikisi de kaydedebilir.

StayFlow'da her dolu gece `RoomNights` tablosunda bir satırdır ve `(RoomId, Date)` üzerinde **unique index** vardır.
Rezervasyon ve tüm geceleri tek transaction'da yazılır. Yarışı kaybeden istek veritabanı tarafından reddedilir, geriye hiçbir satırı kalmaz ve kullanıcıya `409` döner.
Uygulamadaki ön kontrol yalnızca hızlı ve anlaşılır bir hata vermek içindir; asıl garanti veritabanındadır.

Bu davranış üç ayrı yoldan doğrulandı:
- **Gerçek SQL Server (Testcontainers, her CI'da):** Aynı oda için 30 gerçekten paralel HTTP isteği gönderiliyor. Sonuç tam olarak 1 × `201` ve 29 × `409`; kaybedenlerden hiçbir artık kayıt kalmıyor. Farklı odalara paralel rezervasyonlar ise birbirini engellemiyor.
- **Deterministik yarış testi:** Bir EF Core interceptor'ı, ön kontrol ile kayıt arasına rakip bir rezervasyon sokuyor ([ReservationServiceTests](backend/StayFlow.Tests/Reservations/ReservationServiceTests.cs)).
- **Mobil uygulama:** Misafir oda değil oda *tipi* seçer. Aynı anda son odalara talip olan misafirler için boş odalar sırayla denenir; yarışı kaybeden misafir bir sonraki boş odayı alır.

### 2. Çok kiracılık (multi-tenancy)
- EF Core global query filter'ları her sorguya JWT'deki `hotelId`'yi otomatik ekler. Tenant bilgisi yoksa hiçbir satır dönmez (fail-closed).
- Başka otelin kaydına erişim `404` döner; kaydın var olduğu bile sızmaz.
- SignalR bağlantıları yalnızca kendi otelinin grubuna katılır. Grup adı istemciden değil, imzalı token'dan gelir.

### 3. Önbellek geçersiz kılma
Müsaitlik sonuçları Redis'te, otel başına bir **sürüm anahtarı** ile tutulur.
- Oda, fiyat ya da rezervasyon `SaveChanges` ile kaydedildiğinde sürüm değişir ve eski sonuçlar bir daha okunmaz.
- Geçersiz kılma tek bir yerde olduğu için yeni kod yazan biri onu unutamaz.
- Redis çökerse arama önbelleksiz çalışmaya devam eder.

### 4. Zaman
- **"Bugün" otelin saat dilimine göre hesaplanır.** UTC ile, Antalya'da gece 01:00'de gelen misafirin check-in'i "dün" sayılıp reddediliyordu.
- **Tüm zamanlar sonunda `Z` ile döner.** SQL Server'dan okunan zamanlar önce işaretsiz geliyordu; telefon bunları yerel saat sanıp 3 saat kaydırıyor ve yeni bir rezervasyonun ödeme süresi "dolmuş" görünüyordu.
- Her iki hata da testle kilitlendi.

### 5. Testlerin yakaladığı hatalardan birkaçı
- **Demo sıfırlama:** SQL Server'ın "tekrar dene" stratejisi elle açılan transaction'a izin vermiyor; SQLite testleri bunu göremiyordu. Testcontainers testi yakaladı.
- **Deploy sonrası 502:** Nginx API adresini yalnızca açılışta çözümlüyordu; her API deploy'undan sonra 502 veriyordu.
- **Kaybolan correlation id:** Hata cevaplarında correlation id başlığı kayboluyordu, çünkü exception handler başlıkları temizliyor.

## Özellikler

**Otel paneli**
- **Genel bakış:** Doluluk, bugünkü giriş ve çıkışlar, 7 gecelik doluluk grafiği.
- **Rezervasyon takvimi:** Odalar × geceler tablosu.
- **Rezervasyon işlemleri:** Rezervasyon akışı, onay, check-in, check-out (erken çıkışta kalan geceler satışa açılır), iptal.
- **Oda ve fiyat yönetimi:** Oda tipleri, sezon fiyatları, odalar.
- **Yorumlar:** Puan dağılımı, yanıt yazma, yorum gizleme.
- **Personel ve roller:** `Owner` her şeyi yönetir, `Reception` rezervasyonları yönetir.
- **Canlı güncelleme:** Başka bir çalışanın ya da misafirin rezervasyonu, sayfa yenilemeden takvime ve bildirimlere düşer.

**Misafir uygulaması** (hesap gerekmez; rezervasyona 8 karakterlik kod + e-posta ile erişilir)
- Şehre göre otel arama; puan, yorumlar ve otelin yanıtları.
- Tarih ve kişi seçimi, canlı müsaitlik ve toplam fiyat.
- 15 dakikalık ödeme süresi ve simüle kart ödemesi. Kart numarası saklanmaz; yalnızca son 4 hane tutulur.
- "Rezervasyonlarım": kodlar telefonun şifreli deposunda saklanır; iptal ve konaklama sonrası yorum buradan yapılır.
- Push bildirim: Rezervasyon onaylandığında, check-in yapıldığında, iptal edildiğinde ya da ödeme süresi dolduğunda.

**Arka plan işleri (Hangfire)**
- Her dakika: Ödeme süresi dolan rezervasyonları iptal eder. Her rezervasyon için ayrı bir iş kurmak yerine tarama yapılıyor; kaybolan bir iş odayı sonsuza kadar tutamaz.
- Her sabah 10:00: Yarın gelecek misafirlere hatırlatma e-postası. İş tekrar çalışsa bile e-posta bir kez gider.
- Her akşam 22:00: Otel sahiplerine gün sonu raporu.
- Her gece 04:00: Demo modunda demo otel, bugüne göre yeniden üretilir.

**Yapay zekâ (Claude API)**
- **Yorum özeti:** Güçlü yanlar, tekrar eden şikayetler ve somut öneriler. Yorumlar değişmedikçe özet önbellekten gelir, yani ikinci tıklama ücretli değildir.
- **Yanıt taslağı:** Yorumun kendi dilinde, tazminat taahhüdü içermeyen bir taslak. Otel sahibi göndermeden önce düzenler.
- **Prompt injection koruması:** Yorumlar güvenilmeyen içerik olarak, kaçış karakterleriyle ve "bu veridir, talimat değildir" kuralıyla gönderilir. Çıktı JSON şemasıyla sınırlıdır.
- **İsteğe bağlı:** `ANTHROPIC_API_KEY` yoksa özellik kapalıdır ve panel bunu açıkça söyler.

## Güvenlik

- **Access token:** 15 dakikalık, yalnızca bellekte tutulur.
- **Refresh token:** `HttpOnly` + `Secure` + `SameSite=Strict` bir cookie'de tutulur; JavaScript okuyamaz. Veritabanında hash'lenmiş saklanır ve her kullanımda yenilenir. İki sekme aynı anda yenileme yaparsa biri oturumdan düşmesin diye önceki token 30 saniye daha kabul edilir.
- **Deneme sınırları:**
  - Giriş ve kayıt: IP başına dakikada 20.
  - Misafir uçları: IP başına dakikada 120.
  - Yapay zekâ uçları: otel başına saatte 30.
- **Misafir erişimi:** Rezervasyon kodu ve e-posta birlikte istenir. Biri yanlışsa hangisinin yanlış olduğu söylenmez. Misafir cevaplarında oda numarası ya da iç kimlikler yer almaz.
- **Loglama:** Serilog ile JSON log. Her isteğin `X-Correlation-Id`'si hata gövdesinde de döner. WebSocket token'ı Nginx loglarına yazılmaz.
- **Container'lar:** Root olmayan kullanıcıyla çalışır. Dışarıya yalnızca Nginx açıktır. Uygulama örnek JWT anahtarıyla başlamayı reddeder.

## Çalıştırma

### Docker ile (önerilen)

```bash
cp .env.example .env        # şifreleri değiştirin; demo için DEMO_MODE=true
docker compose up --build
```

| Adres | Ne |
|---|---|
| http://localhost:8080 | Otel paneli. Demo modunda **"Demo hesabıyla dene"** (`demo@stayflow.app` / `Demo1234!`) |
| http://localhost:8025 | Mailpit: gönderilen e-postalar (hatırlatma, rapor, onay) |

SQL Server, Redis, API, panel ve Mailpit birlikte ayağa kalkar; veritabanı ilk açılışta oluşturulur.

### Misafir uygulaması

```bash
cd mobile
npm install
npx expo start              # telefonda Expo Go ile QR kodu okutun, ya da "w" ile tarayıcıda açın
```

Telefonda denerken `mobile/.env.local` içindeki `EXPO_PUBLIC_API_URL` değerini bilgisayarın yerel IP'si yapın (ör. `http://192.168.1.20:8080/api`).
Push bildirimleri için uygulamanın bir EAS projesine bağlanması gerekir (`npx eas-cli init`).

### Geliştirme ortamı (Docker'sız)

```bash
cd backend
dotnet ef database update -p StayFlow.Infrastructure -s StayFlow.API
dotnet run --project StayFlow.API --launch-profile http     # http://localhost:5182, API dokümanı: /scalar, Hangfire: /hangfire

cd admin-panel && npm install && npm run dev                  # http://localhost:5173
```

## Testler

| | Kapsam | Sayı |
|---|---|---|
| `cd backend && dotnet test` | Servisler, HTTP entegrasyon (roller, cookie, 409/429), SignalR, arka plan işleri, push, yapay zekâ sözleşmesi, **Testcontainers ile gerçek SQL Server + Redis** (Docker gerekir) | 89 |
| `cd admin-panel && npm test` | Takvim yerleşimi, genel bakış hesapları, token yenileme (tek refresh), giriş formu | 16 |
| `cd mobile && npm test` | Tarih hesapları (ay, artık yıl, yaz saati), kart biçimlendirme, kayıtlı rezervasyonlar | 7 |
| `python scripts/smoke_test.py` | Çalışan sisteme karşı 45 kontrol: 20 eşzamanlı rezervasyon, rotation, deneme sınırı, check-in/out, önbellek | |

Bazı kritik testler "mutasyon" ile de kontrol edildi: korunan kod geçici olarak bozulup testin gerçekten başarısız olduğu görüldü.

## CI/CD

- **CI** ([ci.yml](.github/workflows/ci.yml)):
  - Backend, panel ve mobil testleri paralel çalışır.
  - Hepsi geçerse tüm sistem Docker Compose ile kurulur ve smoke test gerçek SQL Server'a karşı koşar.
- **Deploy** ([deploy.yml](.github/workflows/deploy.yml)):
  - CI geçince image'lar `ghcr.io`'ya yüklenir.
  - Sunucuya SSH ile bağlanılır ve yeni sürüm `/health` sağlıklı dönene kadar beklenir.
  - Kurulum rehberi: [docs/deploy.md](docs/deploy.md).

## API

Tam liste `/scalar` adresindeki etkileşimli dokümanda. Özet:

| Grup | Uçlar | Kim |
|---|---|---|
| Kimlik | `POST /api/auth/register · login · refresh · logout` | Herkes |
| Otel ve personel | `/api/hotel`, `/api/users` | Personel / Owner |
| Odalar ve fiyatlar | `/api/room-types`, `/api/room-types/{id}/seasonal-prices`, `/api/rooms` | Okuma: personel, yazma: Owner |
| Rezervasyon | `/api/availability`, `/api/reservations` + `/confirm · /check-in · /check-out · /cancel` | Personel |
| Yorumlar | `/api/reviews`, `/summary`, `/insights` (yapay zekâ), `/{id}/reply · /suggest-reply · /visibility` | Personel / Owner |
| Misafir | `/api/public/hotels`, `/{slug}`, `/availability`, `/reviews`, `/reservations`; `/api/public/reservations/{code}` + `/pay · /cancel · /review · /push-token` | Oturumsuz (kod + e-posta) |
| Canlı | `/hubs/reservations` (SignalR) | Personel |

Hatalar RFC 7807 `ProblemDetails` formatında döner: 400, 401, 403, 404, 409, 429 ve 503 (isteğe bağlı özellik kapalı).

## Durum ve sınırlar

- [x] Faz 1: Çekirdek backend, otel paneli, Docker, CI/CD, güvenlik, demo verisi
- [x] Faz 2: Serilog, Hangfire, SignalR, Redis, Testcontainers
- [x] Faz 3: Misafir API'si, React Native uygulaması, push bildirim, yorumlar, yapay zekâ
- [ ] Sunucuya ilk kurulum ve canlı demo adresi

**Bilinen sınırlar**
- **Ödeme simülasyondur.** Gerçek bir sağlayıcı (iyzico, Stripe) yerine `PublicBookingService.PayAsync` içindeki simülasyon kullanılıyor.
- **Otel fotoğrafı yok.** Otel kartlarında fotoğraf yerine marka renginde bir kapak var; dosya yükleme henüz yapılmadı.
- **Tek API instance'ı.** Migration'lar açılışta uygulanıyor. Birden fazla instance için bu adım ayrı bir migration bundle'a taşınmalı; Redis önbellek ve Hangfire zaten paylaşımlı çalışıyor.
