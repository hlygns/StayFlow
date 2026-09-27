using Microsoft.EntityFrameworkCore;
using StayFlow.Application.Common.Interfaces;
using StayFlow.Domain.Common;
using StayFlow.Domain.Entities;

namespace StayFlow.Infrastructure.Persistence;

public class StayFlowDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IAvailabilityCacheInvalidator? _availabilityCache;

    public StayFlowDbContext(
        DbContextOptions<StayFlowDbContext> options,
        ICurrentTenantService currentTenantService,
        IAvailabilityCacheInvalidator? availabilityCache = null)
        : base(options)
    {
        _currentTenantService = currentTenantService;
        _availabilityCache = availabilityCache;
    }

    public DbSet<Hotel> Hotels => Set<Hotel>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<SeasonalPrice> SeasonalPrices => Set<SeasonalPrice>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<RoomNight> RoomNights => Set<RoomNight>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // === CIFTE REZERVASYONU ENGELLEYEN KISIT ===
        // Ayni oda + ayni gece kombinasyonu veritabaninda sadece bir kere var olabilir.
        // Iki es zamanli istek ayni satiri eklemeye calisirsa, ikincisi DbUpdateException alir.
        modelBuilder.Entity<RoomNight>()
            .HasIndex(rn => new { rn.RoomId, rn.Date })
            .IsUnique();

        // Giris ekraninda otel secilmiyor (sadece e-posta + sifre), bu yuzden e-posta
        // tum sistemde tekil olmali - yoksa "bu e-posta hangi otelin kullanicisi?" belirsiz kalir.
        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Hotel>()
            .HasIndex(h => h.Slug)
            .IsUnique();

        // Refresh istegi kullaniciyi token hash'inden bulur - tum tabloyu taramasin.
        modelBuilder.Entity<AppUser>().HasIndex(u => u.RefreshToken);
        modelBuilder.Entity<AppUser>().HasIndex(u => u.PreviousRefreshToken);

        // Rezervasyon kodu tum sistemde tekil (misafir oteli secmeden kodla arar)
        modelBuilder.Entity<Reservation>().Property(r => r.ConfirmationCode).HasMaxLength(12);
        modelBuilder.Entity<Reservation>().HasIndex(r => r.ConfirmationCode).IsUnique();
        modelBuilder.Entity<Hotel>().Property(h => h.Description).HasMaxLength(2000);

        // Bir konaklama icin tek yorum
        modelBuilder.Entity<Review>().HasIndex(r => r.ReservationId).IsUnique();
        modelBuilder.Entity<Review>().Property(r => r.Comment).HasMaxLength(2000);
        modelBuilder.Entity<Review>().Property(r => r.OwnerReply).HasMaxLength(2000);
        modelBuilder.Entity<Review>().ToTable(t => t.HasCheckConstraint("CK_Review_Rating", "[Rating] BETWEEN 1 AND 5"));

        // Ayni otelde iki tane "101" numarali oda olamaz.
        modelBuilder.Entity<Room>()
            .HasIndex(r => new { r.HotelId, r.RoomNumber })
            .IsUnique();

        // SeasonalPrice'ta baslangic bitisten once olmali.
        modelBuilder.Entity<SeasonalPrice>()
            .ToTable(t => t.HasCheckConstraint("CK_SeasonalPrice_DateRange", "[StartDate] < [EndDate]"));

        // Para alanlari icin hassasiyet (precision) belirtilmezse SQL Server kusuratlari
        // sessizce kirpabilir. 18 basamak, 2'si ondalik (ör. 123456789012345.67) yeterli.
        modelBuilder.Entity<RoomType>().Property(e => e.BasePrice).HasPrecision(18, 2);
        modelBuilder.Entity<SeasonalPrice>().Property(e => e.PricePerNight).HasPrecision(18, 2);
        modelBuilder.Entity<Reservation>().Property(e => e.TotalPrice).HasPrecision(18, 2);
        modelBuilder.Entity<Payment>().Property(e => e.Amount).HasPrecision(18, 2);

        // === CASCADE DELETE DONGUSUNU ONLEME ===
        // SQL Server ayni tabloya birden fazla "kademeli silme" yolu birakilmasina izin vermiyor
        // (ör. Hotel -> Room dogrudan VE Hotel -> RoomType -> Room dolayli yoldan).
        // Once tum FK'lari "Restrict" yapiyoruz (varsayilan davranis: ust kayit silinemez
        // eger bagli alt kayitlari varsa) - zaten Hotel/Room'da IsActive alani ile soft-delete
        // yapacagiz, gercek DELETE nadiren calisacak bir islem.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // Istisna: bir Reservation silinirse, ona ait RoomNight ve Payment kayitlarinin
        // da silinmesi mantikli - onlar Reservation olmadan anlamsiz, ve bu tek cascade
        // yolu oldugu icin (baska hicbir yerden RoomNight/Payment'a cascade gelmiyor) guvenli.
        modelBuilder.Entity<RoomNight>()
            .HasOne(rn => rn.Reservation)
            .WithMany(r => r.RoomNights)
            .HasForeignKey(rn => rn.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Reservation)
            .WithMany(r => r.Payments)
            .HasForeignKey(p => p.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        // === TENANT IZOLASYONU (multi-tenancy) ===
        // Fail-closed: HotelId bilinmiyorsa (or. HttpContext yok, JWT'de claim yok)
        // hicbir satir donmez. "Bilmiyorsam hepsini goster" degil, "bilmiyorsam hicbirini gosterme".
        // HotelId null ise "e.HotelId == null" hicbir satirla eslesmez (HotelId kolonu null olamaz).
        // Not: "HotelId.HasValue && e.HotelId == HotelId.Value" seklinde yazmak ISE YARAMAZ -
        // EF parametreyi hazirlarken .Value'yu yine de okur ve exception firlatir (bkz. TenantIsolationTests).
        modelBuilder.Entity<AppUser>().HasQueryFilter(e => e.HotelId == _currentTenantService.HotelId);
        modelBuilder.Entity<RoomType>().HasQueryFilter(e => e.HotelId == _currentTenantService.HotelId);
        modelBuilder.Entity<Room>().HasQueryFilter(e => e.HotelId == _currentTenantService.HotelId);
        modelBuilder.Entity<SeasonalPrice>().HasQueryFilter(e => e.HotelId == _currentTenantService.HotelId);
        modelBuilder.Entity<Guest>().HasQueryFilter(e => e.HotelId == _currentTenantService.HotelId);
        modelBuilder.Entity<Reservation>().HasQueryFilter(e => e.HotelId == _currentTenantService.HotelId);
        modelBuilder.Entity<RoomNight>().HasQueryFilter(e => e.HotelId == _currentTenantService.HotelId);
        modelBuilder.Entity<Payment>().HasQueryFilter(e => e.HotelId == _currentTenantService.HotelId);
        modelBuilder.Entity<Review>().HasQueryFilter(e => e.HotelId == _currentTenantService.HotelId);
    }

    /// <summary>
    /// Yeni eklenen her tenant entity'sine giris yapmis kullanicinin HotelId'sini otomatik yazar.
    /// Servis kodunda "HotelId = ..." yazmayi unutmak, kaydi yanlis otele (Guid.Empty) baglamak
    /// demek olurdu - bu sorumlulugu tek bir yere topluyoruz.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>().Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity.HotelId == Guid.Empty)
            {
                entry.Entity.HotelId = _currentTenantService.HotelId
                    ?? throw new InvalidOperationException(
                        $"{entry.Entity.GetType().Name} kaydedilemedi: HotelId belirtilmedi ve istekte tenant bilgisi yok.");
            }
        }

        // Musaitligi etkileyen kayitlar degistiyse o otellerin musaitlik onbellegi gecersiz kilinir
        var affectedHotels = ChangeTracker.Entries<ITenantEntity>()
            .Where(e => (e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                        && (e.Entity is Reservation or RoomNight or Room or RoomType or SeasonalPrice))
            .Select(e => e.Entity.HotelId)
            .Distinct()
            .ToList();

        return SaveAndInvalidateAsync(affectedHotels, cancellationToken);
    }

    private async Task<int> SaveAndInvalidateAsync(List<Guid> affectedHotels, CancellationToken cancellationToken)
    {
        var result = await base.SaveChangesAsync(cancellationToken);
        if (affectedHotels.Count > 0 && _availabilityCache is not null)
        {
            await _availabilityCache.InvalidateAsync(affectedHotels, cancellationToken);
        }
        return result;
    }
}
