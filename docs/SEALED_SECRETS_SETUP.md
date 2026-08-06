# Sealed Secrets — Kurulum

K3s üzerinde Sealed Secrets controller kurulumu. Deploy edilecek manifestler `manifests/` altında, şablon çıktılar overlay katmanında üretilir.

## Deploy

```bash
helm repo add sealed-secrets https://bitnami-labs.github.io/sealed-secrets
helm repo update

helm install sealed-secrets sealed-secrets/sealed-secrets \
  --namespace kube-system \
  --values manifests/sealed-secrets/values.yaml
```

Chart sürümü ile `kubeseal` sürümü aynı major hatta olmalı.

## Anahtar Dışa Aktarımı

```bash
kubeseal --fetch-cert \
  --controller-name sealed-secrets-controller \
  --controller-namespace kube-system \
  > docs/cert/sealed-secrets-cert.pem
```

Private key yedeği, controller'ın kurulu olduğu cluster'dan alınır ve repo dışında saklanır:

```bash
kubectl get secret -n kube-system sealed-secrets-key \
  -o jsonpath='{.data.tls\.key}' | base64 -d > sealed-secrets-key.pem
```

## Doğrulama

```bash
kubectl get pods -n kube-system -l app=sealed-secrets
kubeseal --fetch-cert --controller-name sealed-secrets-controller --controller-namespace kube-system
```

## Secret Şifreleme (K2.12)

Controller kurulduktan sonra:

```bash
# 1. Public key'i al (ilk seferde)
kubeseal --fetch-cert \
  --controller-name sealed-secrets-controller \
  --controller-namespace kube-system \
  > docs/cert/sealed-secrets-cert.pem

# 2. Düz secret'ı şifrele → base'e yaz + kustomization'a ekle
bash scripts/seal-secret.sh \
  manifests/base/datactive-secret.yaml \
  docs/cert/sealed-secrets-cert.pem

# 3. Doğrula (kustomize build → SealedSecret objesi görünür)
kustomize build manifests/base | grep -A2 "kind: SealedSecret"
```

## Yeniden Şifreleme (Secret Güncelleme)

DB şifresi değiştiğinde:

```bash
# 1. Yeni değerle düz secret'ı düzenle (placeholder dosyası)
# 2. Aynı script'i tekrar çalıştır (eski sealed secret üzerine yazar)
bash scripts/seal-secret.sh \
  manifests/base/datactive-secret.yaml \
  docs/cert/sealed-secrets-cert.pem

# 3. Cluster'a uygula (controller çözer, pod'lar yeni değeri alır)
kubectl apply -f manifests/base/datactive-sealed-secret.yaml
```

> Public key DEĞİŞMEDİĞİ sürece aynı cert ile yeniden şifreleme sorunsuzdur.
> Controller yeniden kurulursa (yeni private key) → eski sealed secret'lar çözülemez; önce yedeklenen key geri yüklenmeli.

## Rollback

```bash
helm uninstall sealed-secrets --namespace kube-system
```

Rollback sonrasında controller yeni bir private key üretir; mevcut SealedSecret'lar yedeklenen anahtar geri yüklenmeden çözülemez.
