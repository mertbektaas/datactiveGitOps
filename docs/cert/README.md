# Certificate Store

Bu dizin, Sealed Secrets controller'ının **public key**'ini barındırır.

## Ekleme

Kurulum sonrası `docs/SEALED_SECRETS_SETUP.md` bölümündeki komutla public key alınır ve buraya kaydedilir:

```bash
kubeseal --fetch-cert \
  --controller-name sealed-secrets-controller \
  --controller-namespace kube-system \
  > docs/cert/sealed-secrets-cert.pem
```

## Sınırlamalar

- Bu dizine yalnızca **public key** eklenir.
- **Private key** bu dizine (veya herhangi bir git deposuna) konmaz; güvenli/offline ortamda saklanır.
