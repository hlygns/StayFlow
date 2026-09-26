"""
Calisan bir StayFlow API'sine karsi uctan uca duman testi (smoke test).

    python scripts/smoke_test.py                                  # varsayilan: http://localhost:8080/api
    API=http://localhost:5182/api python scripts/smoke_test.py    # dogrudan API'ye

Her calismada yeni oteller olusturur, mevcut veriye dokunmaz. Sadece Python standart kutuphanesi
kullanir. CI'da docker compose ile ayaga kaldirilan sisteme karsi calisir; en onemli kontrolu
sondaki cifte rezervasyon testidir (ayni oda + ayni geceler icin 20 es zamanli istek).
"""
import json
import os
import sys
import time
import urllib.error
import urllib.request
import uuid
from concurrent.futures import ThreadPoolExecutor
from datetime import date, timedelta

BASE = os.environ.get("API", "http://localhost:8080/api")
RUN = uuid.uuid4().hex[:8]
failures = 0


def day(offset: int) -> str:
    """Bugunden itibaren N gun sonrasi, 'yyyy-MM-dd'. Testler takvime bagli kalmasin."""
    return (date.today() + timedelta(days=offset)).isoformat()


def call(method, path, body=None, token=None):
    data = json.dumps(body).encode("utf-8") if body is not None else None
    request = urllib.request.Request(BASE + path, data=data, method=method)
    request.add_header("Content-Type", "application/json; charset=utf-8")
    if token:
        request.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            raw = response.read().decode()
            return response.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as error:
        raw = error.read().decode()
        try:
            return error.code, (json.loads(raw) if raw else None)
        except json.JSONDecodeError:
            return error.code, raw


def refresh(cookie):
    """Refresh token httpOnly cookie'de: tarayicinin yaptigini elle yapiyoruz (Cookie basligi)."""
    request = urllib.request.Request(BASE + "/auth/refresh", data=b"", method="POST")
    if cookie:
        request.add_header("Cookie", f"stayflow_refresh={cookie}")
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return response.status, json.loads(response.read().decode()), refresh_cookie(response.headers)
    except urllib.error.HTTPError as error:
        return error.code, None, None


def refresh_cookie(headers):
    for header in headers.get_all("Set-Cookie") or []:
        if header.startswith("stayflow_refresh="):
            return header.split(";", 1)[0].split("=", 1)[1]
    return None


def check(name, actual, expected, detail=None):
    global failures
    ok = actual == expected
    failures += 0 if ok else 1
    print(f"[{'OK  ' if ok else 'FAIL'}] {name}: {actual} (beklenen {expected})" + ("" if ok or detail is None else f" -> {detail}"))


def wait_for_api():
    for _ in range(60):
        try:
            call("POST", "/auth/login", {"email": "x@x.com", "password": "x"})
            return
        except (urllib.error.URLError, ConnectionError, TimeoutError):
            time.sleep(2)
    sys.exit(f"API'ye ulasilamadi: {BASE}")


def register(hotel_name, email):
    """Kayit olur; (cevap, refresh cookie degeri, ham Set-Cookie basligi) dondurur."""
    request = urllib.request.Request(BASE + "/auth/register", method="POST", data=json.dumps({
        "hotelName": hotel_name, "city": "Antalya", "ownerFullName": "Test Sahibi",
        "ownerEmail": email, "password": "Parola123!",
    }).encode("utf-8"))
    request.add_header("Content-Type", "application/json; charset=utf-8")
    with urllib.request.urlopen(request, timeout=30) as response:
        check(f"otel kaydi ({hotel_name})", response.status, 200)
        raw_cookie = next((h for h in response.headers.get_all("Set-Cookie") or [] if h.startswith("stayflow_refresh=")), "")
        return json.loads(response.read().decode()), refresh_cookie(response.headers), raw_cookie


wait_for_api()
print(f"API: {BASE}\n")

# ---------- Kimlik dogrulama ve roller ----------
reg, cookie_a, raw_cookie = register("Smoke Otel", f"owner-{RUN}@smoke.test")
owner = reg["accessToken"]
check("refresh token govdede yok", "refreshToken" in reg, False)
check("refresh cookie HttpOnly + SameSite=Strict + Path=/api/auth",
      all(x in raw_cookie.lower() for x in ("httponly", "samesite=strict", "path=/api/auth")), True, raw_cookie)
check("ayni e-posta ile ikinci kayit", call("POST", "/auth/register", {
    "hotelName": "X", "city": "X", "ownerFullName": "X", "ownerEmail": f"owner-{RUN}@smoke.test", "password": "Parola123!"})[0], 409)
check("token'siz istek", call("GET", "/hotel")[0], 401)
check("yanlis sifre", call("POST", "/auth/login", {"email": f"owner-{RUN}@smoke.test", "password": "yanlis"})[0], 401)

status, _ = call("POST", "/users", {"fullName": "Resepsiyon", "email": f"rec-{RUN}@smoke.test", "password": "Parola123!", "role": "Reception"}, owner)
check("resepsiyon kullanicisi olustur", status, 200)
reception = call("POST", "/auth/login", {"email": f"rec-{RUN}@smoke.test", "password": "Parola123!"})[1]["accessToken"]
check("resepsiyon personel listesine erisemez", call("GET", "/users", token=reception)[0], 403)
check("resepsiyon oda tipi olusturamaz", call("POST", "/room-types", {"name": "X", "capacity": 1, "basePrice": 1}, reception)[0], 403)

status, refreshed, cookie_b = refresh(cookie_a)
check("refresh (cookie ile)", status, 200)
check("refresh yeni bir cookie verir (rotation)", cookie_b is not None and cookie_b != cookie_a, True)
# Iki sekme ayni anda: ikincisi eski cookie'yi gonderir - 30 sn tolerans icinde kabul edilir
check("iki sekme ayni anda yeniler: eski cookie tolerans icinde", refresh(cookie_a)[0], 200)
# A artik "onceki" bile degil (once B, sonra C uretildi) -> reddedilir
check("iki rotasyon onceki cookie reddedilir", refresh(cookie_a)[0], 401)
check("cookie'siz refresh", refresh(None)[0], 401)
owner = refreshed["accessToken"]

# ---------- Oda tipleri, odalar, fiyatlar ----------
status, std = call("POST", "/room-types", {"name": "Standart", "capacity": 2, "basePrice": 1000}, owner)
check("oda tipi olustur", status, 201, std)
status, sea = call("POST", "/room-types", {"name": "Deniz Manzarali", "capacity": 3, "basePrice": 2000}, owner)
rooms = {}
for number, room_type in [("101", std), ("102", std), ("201", sea)]:
    status, room = call("POST", "/rooms", {"roomNumber": number, "floor": int(number[0]), "roomTypeId": room_type["id"]}, owner)
    check(f"oda {number}", status, 201, room)
    rooms[number] = room
check("ayni oda numarasi", call("POST", "/rooms", {"roomNumber": "101", "floor": 1, "roomTypeId": std["id"]}, owner)[0], 409)

# Sezon: 12. gunden 20. gune kadar (dahil) gecelik 3000
check("sezon fiyati", call("POST", f"/room-types/{sea['id']}/seasonal-prices",
      {"startDate": day(12), "endDate": day(20), "pricePerNight": 3000}, owner)[0], 201)
check("cakisan sezon fiyati", call("POST", f"/room-types/{sea['id']}/seasonal-prices",
      {"startDate": day(18), "endDate": day(25), "pricePerNight": 2500}, owner)[0], 409)

# ---------- Musaitlik ve fiyat ----------
status, availability = call("GET", f"/availability?checkIn={day(10)}&checkOut={day(13)}&guests=3", token=reception)
check("musaitlik sorgusu", status, 200, availability)
sea_availability = next(x for x in availability if x["roomTypeId"] == sea["id"])
check("fiyat: 2 gece baz (2000) + 1 gece sezon (3000)", int(sea_availability["totalPrice"]), 7000)
check("3 kisilik aramada 2 kisilik tip listelenmez", any(x["roomTypeId"] == std["id"] for x in availability), False)

# ---------- Rezervasyon ----------
guest = {"fullName": "Ayşe Yılmaz", "phone": "5551112233"}
room_101 = rooms["101"]["id"]
status, first = call("POST", "/reservations", {"roomId": room_101, "checkIn": day(30), "checkOut": day(33), "guest": guest}, reception)
check("rezervasyon olustur", status, 201, first)
check("cakisan tarihler reddedilir", call("POST", "/reservations",
      {"roomId": room_101, "checkIn": day(31), "checkOut": day(32), "guest": guest}, reception)[0], 409)
check("cikis gunu ayni odaya giris yapilabilir", call("POST", "/reservations",
      {"roomId": room_101, "checkIn": day(33), "checkOut": day(34), "guest": guest}, reception)[0], 201)
check("gecmis tarih reddedilir", call("POST", "/reservations",
      {"roomId": room_101, "checkIn": day(-3), "checkOut": day(-1), "guest": guest}, reception)[0], 400)
check("iptal", call("POST", f"/reservations/{first['id']}/cancel", token=reception)[0], 200)
check("iptalden sonra ayni geceler tekrar satilir", call("POST", "/reservations",
      {"roomId": room_101, "checkIn": day(30), "checkOut": day(33), "guest": guest}, reception)[0], 201)

# ---------- Check-in / check-out ----------
status, stay = call("POST", "/reservations", {"roomId": rooms["102"]["id"], "checkIn": day(0), "checkOut": day(3), "guest": guest}, reception)
check("bugun baslayan rezervasyon", status, 201, stay)
check("onaylanmadan check-in yapilamaz", call("POST", f"/reservations/{stay['id']}/check-in", token=reception)[0], 409)
call("POST", f"/reservations/{stay['id']}/confirm", token=reception)
check("check-in", call("POST", f"/reservations/{stay['id']}/check-in", token=reception)[0], 200)
check("check-in yapilmis rezervasyon iptal edilemez", call("POST", f"/reservations/{stay['id']}/cancel", token=reception)[0], 409)
status, checked_out = call("POST", f"/reservations/{stay['id']}/check-out", token=reception)
check("erken check-out", status, 200, checked_out)
check("erken check-out: konaklama 1 geceye iner", checked_out["nights"], 1)
check("bosalan geceler tekrar satilir", call("POST", "/reservations",
      {"roomId": rooms["102"]["id"], "checkIn": day(1), "checkOut": day(3), "guest": guest}, reception)[0], 201)

# ---------- Oteller arasi izolasyon ----------
other = register("Baska Otel", f"other-{RUN}@smoke.test")[0]["accessToken"]
check("baska otelin odasini goremez", call("GET", f"/rooms/{room_101}", token=other)[0], 404)
check("baska otelin rezervasyonunu goremez", call("GET", f"/reservations/{first['id']}", token=other)[0], 404)
check("baska otelin odasina rezervasyon yapamaz", call("POST", "/reservations",
      {"roomId": room_101, "checkIn": day(40), "checkOut": day(41), "guest": guest}, other)[0], 404)

# ---------- Cifte rezervasyon: 20 es zamanli istek ----------
def book(_):
    return call("POST", "/reservations", {"roomId": rooms["201"]["id"], "checkIn": day(50),
                                          "checkOut": day(53), "guest": guest}, reception)[0]

with ThreadPoolExecutor(max_workers=20) as pool:
    results = list(pool.map(book, range(20)))
print(f"       es zamanli sonuclar: {sorted(results)}")
check("20 es zamanli istekten basarili olan", results.count(201), 1)
check("20 es zamanli istekten 409 alan", results.count(409), 19)

# ---------- Deneme sinirlamasi (en sonda: bu IP'yi bir dakikaligina kilitler) ----------
codes = [call("POST", "/auth/login", {"email": f"yok-{RUN}@smoke.test", "password": "yanlis"})[0] for _ in range(25)]
check("25 hizli giris denemesinde sinir devreye girer (429)", 429 in codes, True, codes)

print(f"\n{'BASARILI' if failures == 0 else f'{failures} KONTROL BASARISIZ'}")
sys.exit(1 if failures else 0)
