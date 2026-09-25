# StayFlow

Butik oteller ve pansiyonlar için çok kiracılı (multi-tenant) rezervasyon ve misafir yönetim sistemi.
Her otel kendi hesabıyla sisteme kaydolur; odalarını, fiyatlarını ve rezervasyonlarını yönetir ve yalnızca kendi verisini görür.

> Durum: **1. hafta tamamlandı**: backend çekirdeği (auth, oda/fiyat yönetimi, müsaitlik, rezervasyon) ve testler.
> Sırada: React + TypeScript yönetim paneli, Docker Compose, CI/CD.

## Teknolojiler

.NET 10 · ASP.NET Core Web API · EF Core 10 · SQL Server · JWT + refresh token · xUnit

## Öne çıkan mühendislik problemi: çifte rezervasyon

İki misafir aynı anda aynı odayı aynı tarihler için ayırtmaya çalışırsa ne olur?
"Önce kontrol et, sonra kaydet" yaklaşımı tek başına yetmez: iki istek de kontrolü aynı anda geçip ikisi de kaydedebilir.

StayFlow'da her dolu gece `RoomNights` tablosunda bir satırdır ve `(RoomId, Date)` üzerinde **unique index** vardır.
Rezervasyon ve tüm geceleri tek transaction'da yazılır. Böylece yarışı kaybeden istek veritabanı seviyesinde reddedilir ve geriye hiçbir satırı kalmaz.
Uygulamadaki ön kontrol yalnızca kullanıcıya hızlı ve anlaşılır bir hata vermek içindir; asıl garanti veritabanındadır.

Bu davranış iki şekilde doğrulandı:

- **Otomatik test** (`ReservationServiceTests.Es_zamanli_iki_istekten_sadece_biri_basarili_olur`): Bir EF Core interceptor'ı, ön kontrol ile kayıt arasına rakip bir rezervasyon sokar. Unique index ikinci isteği reddeder ve 409 döner.
- **Gerçek yük** (SQL Server LocalDB): Aynı oda ve aynı geceler için 20 eşzamanlı HTTP isteği gönderildi. Sonuç 1 × `201 Created`, 19 × `409 Conflict` oldu. Bu 409'lardan bazıları ön kontrolü geçen, ama unique index tarafından reddedilen isteklerdir.

İptal edilen rezervasyonun `RoomNights` satırları silinir; o geceler yeniden satışa açılır.

## Mimari

```
backend/
├── StayFlow.Domain          → Entity'ler (Hotel, Room, Reservation, RoomNight...) ve enum'lar
├── StayFlow.Application     → Servisler, DTO'lar, iş kuralları, IApplicationDbContext
├── StayFlow.Infrastructure  → EF Core DbContext, migration'lar, şifre hashleme
├── StayFlow.API             → Controller'lar, JWT, hata yönetimi (ProblemDetails)
└── StayFlow.Tests           → xUnit + SQLite in-memory testler
```

- **Multi-tenancy:** Her tenant entity'si `ITenantEntity` uygular. EF Core global query filter'ları, sorguları JWT'deki `hotelId` claim'ine göre otomatik filtreler. Tenant bilgisi yoksa hiçbir satır dönmez (fail-closed). Yeni kayıtlara `HotelId` otomatik atanır.
- **Başka otelin verisine erişim** 404 döner: filtre kaydı tamamen gizler, var olduğu bile sızmaz.
- **Kimlik doğrulama:** Kısa ömürlü access token (15 dk) ve refresh token (7 gün). Refresh token'lar veritabanında hash'lenmiş tutulur ve her kullanımda yenilenir (rotation).
- **Roller:** `Owner` (otel sahibi: oda, fiyat, personel yönetimi) ve `Reception` (rezervasyon işlemleri).

## Çalıştırma

Gereksinimler: .NET 10 SDK ve SQL Server LocalDB (Visual Studio ile birlikte gelir) ya da herhangi bir SQL Server.

```bash
cd backend
dotnet tool install --global dotnet-ef   # bir kere
dotnet ef database update -p StayFlow.Infrastructure -s StayFlow.API
dotnet run --project StayFlow.API --launch-profile http
```

API `http://localhost:5182` adresinde açılır. Örnek istekler [StayFlow.API.http](backend/StayFlow.API/StayFlow.API.http) dosyasındadır.

> `appsettings.Development.json` içindeki JWT anahtarı yalnızca yerel geliştirme içindir.
> Canlı ortamda anahtar `Jwt__SecretKey` ortam değişkeniyle verilmelidir (en az 32 karakter).

Testler:

```bash
cd backend
dotnet test
```

## API

| Metot | Yol | Yetki |
|---|---|---|
| POST | `/api/auth/register` · `/login` · `/refresh` | Herkes |
| POST | `/api/auth/logout` | Giriş yapmış |
| GET / PUT | `/api/hotel` | Personel / Owner |
| GET / POST | `/api/users` | Owner |
| GET / POST / PUT / DELETE | `/api/room-types` | Okuma: personel, yazma: Owner |
| GET / POST / PUT / DELETE | `/api/room-types/{id}/seasonal-prices` | Okuma: personel, yazma: Owner |
| GET / POST / PUT / DELETE | `/api/rooms` (DELETE = pasife alma) | Okuma: personel, yazma: Owner |
| GET | `/api/availability?checkIn=&checkOut=&guests=` | Personel |
| GET / POST | `/api/reservations` | Personel |
| POST | `/api/reservations/{id}/confirm` · `/cancel` | Personel |

Hatalar RFC 7807 `ProblemDetails` formatında döner: 400 (doğrulama), 401, 403, 404, 409 (çakışma).

## Yol haritası

- [x] **1. hafta:** Auth ve roller, multi-tenancy, oda tipi/oda/sezon fiyatı, müsaitlik, rezervasyon ve çifte rezervasyon koruması, testler
- [ ] React + TypeScript yönetim paneli (rezervasyon takvimi, oda ve fiyat yönetimi)
- [ ] Docker Compose, GitHub Actions CI, VPS + Nginx ile canlı yayın
- [ ] Hangfire (ödenmeyen rezervasyonu 15 dk'da iptal), SignalR, Redis, Testcontainers, Serilog
- [ ] React Native misafir uygulaması, LLM ile yorum özetleme
