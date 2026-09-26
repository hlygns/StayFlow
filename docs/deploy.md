# StayFlow'u bir VPS'e kurma

Bu rehber StayFlow'u Ubuntu 24.04 çalışan bir sunucuda HTTPS ile yayına alır ve sonraki güncellemeleri
GitHub Actions ile otomatik hâle getirir. Bir kez yapılır; toplam 30–45 dakika sürer.

## Mimari

```
İnternet ──HTTPS──▶ Nginx (sunucu, Let's Encrypt) ──▶ 127.0.0.1:8080 ──▶ [web] panel + /api proxy
                                                                         └─▶ [api] ASP.NET Core ──▶ [db] SQL Server
```

- Dışarıya yalnızca 22 (SSH), 80 ve 443 portları açıktır.
- Panel container'ı sadece `127.0.0.1`'e bağlanır; API ve veritabanı hiçbir porttan dışarı açılmaz.
- TLS sunucudaki Nginx'te sonlanır; API isteğin HTTPS olduğunu `X-Forwarded-Proto` başlığından öğrenir.

## Gerekenler

- **VPS:** en az **2 vCPU / 4 GB RAM** (SQL Server tek başına ~2 GB ister) ve 20 GB disk. Hetzner, DigitalOcean, Turhost ya da benzeri.
- **Alan adı:** Örneğin `stayflow.alanadiniz.com`. DNS'te bu ad için sunucunun IP'sine giden bir **A kaydı** olmalı.
- **GitHub reposu:** Bu proje GitHub'a push edilmiş olmalı.

## 1. Sunucuyu hazırla

```bash
ssh root@SUNUCU_IP

# Güncellemeler + güvenlik duvarı
apt update && apt upgrade -y
ufw allow OpenSSH && ufw allow 'Nginx Full' && ufw --force enable

# Docker (resmî kurulum betiği)
curl -fsSL https://get.docker.com | sh

# Uygulamayı çalıştıracak kullanıcı (root yerine)
adduser --disabled-password --gecos "" deploy
usermod -aG docker deploy

# Nginx + Certbot
apt install -y nginx certbot python3-certbot-nginx
```

> **Not:** Docker, `ports:` ile yayınlanan portlar için ufw kurallarını atlar. Bu yüzden `docker-compose.prod.yml`
> paneli yalnızca `127.0.0.1`'e bağlar. Bu satırı değiştirirseniz panel güvenlik duvarını aşıp doğrudan internete açılır.

## 2. Projeyi sunucuya al

```bash
su - deploy
git clone https://github.com/KULLANICI_ADI/StayFlow.git stayflow
cd stayflow
cp .env.example .env
nano .env
```

`.env` dosyasını doldurun:

| Değişken | Değer |
|---|---|
| `DB_PASSWORD` | Güçlü bir şifre: büyük/küçük harf, rakam ve sembol içermeli |
| `JWT_SECRET` | `openssl rand -base64 48` çıktısı |
| `WEB_PORT` | `8080` (Nginx buna yönlendirir) |
| `IMAGE_OWNER` | GitHub kullanıcı adınız, **küçük harflerle** (imajlar `ghcr.io/<bu>/stayflow-*`) |

```bash
chmod 600 .env   # sadece deploy kullanıcısı okuyabilsin
```

Repo private ise sunucunun imajları çekebilmesi için GHCR'ye bir kez giriş yapın. Token, `read:packages` yetkili bir
GitHub Personal Access Token olmalı:

```bash
echo GITHUB_TOKEN | docker login ghcr.io -u KULLANICI_ADI --password-stdin
```

## 3. Nginx ve HTTPS

```bash
# root olarak
cp /home/deploy/stayflow/deploy/nginx/stayflow.conf /etc/nginx/sites-available/stayflow
nano /etc/nginx/sites-available/stayflow        # server_name satırına kendi alan adınızı yazın
ln -s /etc/nginx/sites-available/stayflow /etc/nginx/sites-enabled/
rm -f /etc/nginx/sites-enabled/default
nginx -t && systemctl reload nginx

# Let's Encrypt sertifikası: certbot 443 bloğunu ve HTTP→HTTPS yönlendirmesini kendisi ekler
certbot --nginx -d stayflow.alanadiniz.com --redirect -m eposta@adresiniz.com --agree-tos -n
```

Sertifika her 90 günde bir otomatik yenilenir (`systemctl list-timers | grep certbot` ile kontrol edebilirsiniz).

## 4. GitHub Actions ile otomatik deploy

1. Sunucuda deploy kullanıcısı için bir SSH anahtarı oluşturun:
   ```bash
   su - deploy
   ssh-keygen -t ed25519 -f ~/.ssh/github_deploy -N ""
   cat ~/.ssh/github_deploy.pub >> ~/.ssh/authorized_keys
   cat ~/.ssh/github_deploy          # bu ÖZEL anahtarı kopyalayın
   ```
2. GitHub'da **Settings → Secrets and variables → Actions** sayfasına gidin:
   - **Secrets:** `VPS_HOST` (sunucu IP'si veya alan adı), `VPS_USER` (`deploy`), `VPS_SSH_KEY` (yukarıdaki özel anahtar)
   - **Variables:** `DEPLOY_ENABLED` = `true`
3. **Settings → Environments** sayfasından `production` ortamını oluşturun. İsterseniz burada "Required reviewers" ile her deploy'u onaya bağlayabilirsiniz.

Bundan sonra `main` (veya `master`) dalına yapılan her push şöyle işler:

1. **CI:** Backend testleri, frontend testleri ve Docker'da uçtan uca test çalışır.
2. **Deploy:** CI geçerse imajlar `ghcr.io`'ya yüklenir.
3. Sunucuda `deploy/deploy.sh <commit>` çalışır. Yeni sürüm `/health` "Healthy" dönene kadar beklenir.

İlk kurulumu elle başlatmak için **Actions → Deploy → Run workflow** kullanın.

## Günlük işlemler

```bash
cd ~/stayflow
C="docker compose -f docker-compose.yml -f docker-compose.prod.yml"

$C ps                          # durum
$C logs -f api                 # API logları
./deploy/deploy.sh a1b2c3d     # belirli bir sürüme geri dön (commit kısa hash'i)
```

### Veritabanı yedeği

SQL Server verisi `stayflow_db-data` Docker volume'unda durur. Günlük yedek için `crontab -e` ile şu satırı ekleyin:

```cron
0 3 * * * docker exec stayflow-db-1 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$(grep DB_PASSWORD ~/stayflow/.env | cut -d= -f2)" -Q "BACKUP DATABASE StayFlow TO DISK='/var/opt/mssql/backup-$(date +\%a).bak' WITH INIT"
```

Bu komut haftanın her günü için bir dosya tutar (7 günlük döngü). Yedekleri ayrıca sunucu dışına (ör. S3 veya Backblaze) kopyalamanız önerilir.

## Sorun giderme

| Belirti | Olası neden |
|---|---|
| `api` sürekli `unhealthy` | `.env` içinde `JWT_SECRET` 32 karakterden kısa ya da `DB_PASSWORD` SQL Server kurallarına uymuyor → `$C logs api` |
| `db` açılmıyor | RAM yetersiz (en az 2 GB boş bellek gerekir) → `free -h` |
| 502 Bad Gateway | Container'lar ayakta değil → `$C ps`, `$C up -d` |
| Deploy'da `denied` hatası | Sunucu GHCR'ye giriş yapmamış (private repo) → 2. adımdaki `docker login` |
