#!/usr/bin/env bash
# Sunucuda calisir: yeni imajlari ceker ve sistemi sifir mudahaleyle gunceller.
#   ./deploy/deploy.sh            -> latest
#   ./deploy/deploy.sh 3f2a9c1    -> belirli bir commit'in imajlari (geri donus icin)
set -euo pipefail

cd "$(dirname "$0")/.."

export IMAGE_TAG="${1:-latest}"
COMPOSE=(docker compose -f docker-compose.yml -f docker-compose.prod.yml)

echo "==> Imajlar cekiliyor (tag: $IMAGE_TAG)"
"${COMPOSE[@]}" pull api web

echo "==> Container'lar guncelleniyor"
# --wait: API /health "Healthy" donene kadar bekler; saglikli degilse hata ile biter
"${COMPOSE[@]}" up --detach --no-build --wait --wait-timeout 300

echo "==> Kullanilmayan eski imajlar temizleniyor"
docker image prune --force >/dev/null

"${COMPOSE[@]}" ps
echo "==> Tamam: $IMAGE_TAG yayinda"
