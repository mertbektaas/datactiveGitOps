# Sealed Secrets — Doğrulama

Sealed Secrets zincirinin çalıştığını doğrulamak için `manifests/sealed-secrets/test-sealedsecret.sh` çalıştırılır.

## Test Akışı

Script; dummy bir secret üretir, public key ile şifreler, SealedSecret'ı cluster'a uygular ve controller'ın çözdüğü değeri doğrular.

- **Çıktı:** `manifests/sealed-secrets/test-sealedsecret.yaml`
- **Doğrulanan:** Şifreli dosyada düz metin yok; çözülen değer dummy değerle eşleşiyor

## Komut

```bash
bash manifests/sealed-secrets/test-sealedsecret.sh
```

Başarılı sonuç: `OK: Secret çözüldü -> testvalue-dummy-secret`

## Notlar

- Public key (`docs/cert/sealed-secrets-cert.pem`) yoksa controller'dan otomatik çekilir.
- Script her çalıştığında aynı `testvalue` secret'ını yeniden üretir; tekrar çalıştırmada `kubectl apply` mevcut kaynağı günceller.
