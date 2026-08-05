# Sertifika Klasörü

Bu klasör, Sealed Secrets controller'ının **public key**'ini içerir.

## Kurulum Sonrası

Cluster yöneticisi (boss) `docs/SEALED_SECRETS_SETUP.md` rehberindeki 5. adımı uyguladıktan sonra public key buraya kaydedilir:

```bash
kubeseal --fetch-cert \
  --controller-name sealed-secrets-controller \
  --controller-namespace kube-system \
  > docs/cert/sealed-secrets-cert.pem
```

## Önemli

- **Public key** herkese açıktır → repo'da durması GÜVENLİDİR
- **Private key** asla buraya konmaz → sadece controller'da, yedekleme güvenli yerde yapılır
