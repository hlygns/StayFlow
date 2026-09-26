# === 1. asama: React uygulamasini derle ===
FROM node:22-alpine AS build
WORKDIR /app

# package*.json degismediyse npm ci katmani onbellekten gelir
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
# Demo kurulumunda giris ekraninda "Demo hesabiyla dene" butonu cikar (bkz. LoginPage)
ARG VITE_DEMO_LOGIN=false
ENV VITE_DEMO_LOGIN=$VITE_DEMO_LOGIN
RUN npm run build

# === 2. asama: derlenmis statik dosyalari Nginx ile sun ===
FROM nginx:1.29-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
