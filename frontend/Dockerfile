# Stage 1: Build Expo web
FROM node:20-alpine AS build
WORKDIR /app

# Expo CLI gereksinimleri
RUN apk add --no-cache git python3 make g++

# Bağımlılıkları kopyala ve kur
COPY package.json package-lock.json* ./
RUN npm install

# Kaynak kodu kopyala
COPY . .

# API adresi env'den (build zamanı)
ARG EXPO_PUBLIC_API_URL=http://localhost:8080
ENV EXPO_PUBLIC_API_URL=$EXPO_PUBLIC_API_URL

# Expo web export (static build)
RUN npx expo export --platform web

# Stage 2: Nginx serve
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html

# Expo web SPA için nginx config (client-side routing)
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
