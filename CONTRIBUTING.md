# Katkı Kuralları

## Branch Stratejisi

- **main**: Kararlı sürüm. Doğrudan push YASAK.
- **feat/***: Özellik dalları. Her görev için `feat/{ticket}-{kisa-aciklama}` formatında branch açılır.
  - Örnek: `feat/K1-5-auth`, `feat/C0-2-klasor-iskleti`

## İş Akışı

1. İlgili FAZ branch'inden feature branch aç
2. Geliştirmeni yap
3. PR aç → Review → Onay
4. FAZ branch'ine merge et

## FAZ Branch Yapısı

Her FAZ için ayrı branch:
- `faz0` — Kurulum & Sözleşme
- `faz1` — Temel Altyapı
- `faz2` — CI/CD Runner
- ...

## Kurallar

- main'e doğrudan push YOK
- PR olmadan merge YOK
- .env ve secret dosyaları repo'ya KONMAZ (.gitignore'da)
- CONTRACT.md değişiklikleri her iki ekip onayıyla
