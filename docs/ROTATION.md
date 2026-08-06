# Secret Rotation — Prosedür

## Şifre Değişimi (Sık)

```bash
# 1. Düz secret'ı yeni değerle güncelle
#    manifests/base/datactive-secret.yaml

# 2. Şifrele + base'e yaz + kustomization'a ekle
bash scripts/seal-secret.sh \
  manifests/base/datactive-secret.yaml \
  docs/cert/sealed-secrets-cert.pem

# 3. Commit + push (ArgoCD otomatik sync eder)
git add manifests/base/datactive-sealed-secret.yaml
git commit -m "chore: rotate DB password"
git push
```

**Not:** SealedSecret değişince pod'lar eski env'i tutar — deployment restart gerekir (ArgoCD sync'te image değişmiyorsa `kubectl rollout restart` manuel).

## Controller Key Sızdıysa (Nadir)

```bash
# Controller'ı yeniden kur → yeni key üretilir
helm uninstall sealed-secrets --namespace kube-system
helm install sealed-secrets sealed-secrets/sealed-secrets \
  --namespace kube-system \
  --values manifests/sealed-secrets/values.yaml

# TÜM SealedSecret'lar yeni public key ile yeniden şifrelenir
# (eski private key gittiği için eski şifreli veriler çözülemez)
```

## Private Key Yedekleme

```bash
kubectl get secret sealed-secrets-key -n kube-system \
  -o jsonpath='{.data.tls\.key}' | base64 -d > sealed-secrets-private-key.pem
```

Yedek güvenli yerde (1Password/Vault). **Repo'ya KONMAZ.** Kaybolursa tüm secret'lar çözülemez.

## Public Key (Repo'da)

```bash
kubeseal --fetch-cert \
  --controller-name sealed-secrets-controller \
  --controller-namespace kube-system \
  > docs/cert/sealed-secrets-cert.pem
```

## Secret RBAC

- **Uygulama pod'u:** sadece kendi namespace'indeki kendi secret'ı
- **ci-builder (ArgoCD):** secret okuyamaz (AppProject whitelist — K2.19)
- **Geliştirici:** SealedSecret üretebilir (public key), düz Secret'a erişemez

## Gerçek Secret Dönüşümü (İlk Kurulum)

```bash
# Gerçek şifrelerle düz secret oluştur (lokal, repo'ya değil)
kubeseal --format yaml --cert docs/cert/sealed-secrets-cert.pem \
  < /tmp/real-secret.yaml > manifests/base/datactive-sealed-secret.yaml

# Düz secret'ı sil (repo'ya girmez), sealed'ı commit et
```
