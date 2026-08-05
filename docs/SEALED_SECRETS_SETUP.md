# Sealed Secrets — Deployment Guide

Bu doküman, Sealed Secrets controller'ının k3s cluster'ına Helm tabanlı kurulumunu tanımlar.

- **Kurulum yapan:** Cluster yöneticisi
- **Yöntem:** Helm chart (`bitnami/sealed-secrets`)
- **Hedef namespace:** `kube-system`

---

## Ön Koşullar

- Çalışan k3s cluster
- `kubectl` cluster'a bağlı, admin yetkisi
- Helm v3+

## Kurulum

### 1. Repository Ekle

```bash
helm repo add sealed-secrets https://bitnami-labs.github.io/sealed-secrets
helm repo update
```

### 2. Controller Kurulumu

```bash
helm install sealed-secrets sealed-secrets/sealed-secrets \
  --namespace kube-system \
  --values manifests/sealed-secrets/values.yaml
```

Kullanılan değerler:

```yaml
# manifests/sealed-secrets/values.yaml
fullnameOverride: sealed-secrets-controller

controller:
  service:
    type: ClusterIP
```

`fullnameOverride` ile controller adı `sealed-secrets-controller` olarak sabitlenir (kubeseal varsayılanında bu ad beklenir).

### 3. kubeseal CLI

```bash
# macOS
brew install kubeseal

# Linux (v0.27.1 örneği)
curl -Lo kubeseal.tar.gz \
  https://github.com/bitnami-labs/sealed-secrets/releases/download/v0.27.1/kubeseal-0.27.1-linux-amd64.tar.gz
tar -xzf kubeseal.tar.gz kubeseal
sudo install -m 755 kubeseal /usr/local/bin/kubeseal
```

Not: kubeseal sürümü, controller chart sürümüyle uyumlu olmalıdır.

## Anahtar Yönetimi

### Public Key (Açık Anahtar)

Geliştiricilerin şifreleme yapabilmesi için controller'ın public key'i repo'ya alınır:

```bash
kubeseal --fetch-cert \
  --controller-name sealed-secrets-controller \
  --controller-namespace kube-system \
  > docs/cert/sealed-secrets-cert.pem
```

Dosya `docs/cert/` altında versiyonlanır.

### Private Key (Gizli Anahtar)

Controller kurulumunda üretilen özel anahtar, mevcut SealedSecret'ların çözülmesi için zorunludur. Aşağıdaki senaryolarda kaybı geri döndürülemez:

- Cluster yeniden kurulumu
- Controller'ın yeniden deploy edilmesi

Anahtar yedeği:

```bash
kubectl get secret -n kube-system sealed-secrets-key \
  -o jsonpath='{.data.tls\.key}' | base64 -d > sealed-secrets-key.pem
```

Yedek, güvenli/offline bir ortamda saklanmalıdır. Repository dışında tutulur.

## Doğrulama

```bash
# Pod durumu
kubectl get pods -n kube-system -l app=sealed-secrets

# Public key erişimi
kubeseal --fetch-cert \
  --controller-name sealed-secrets-controller \
  --controller-namespace kube-system
```

## Rollback

```bash
helm uninstall sealed-secrets --namespace kube-system
```

Rollback sonrası yeni controller farklı bir private key üretir; mevcut SealedSecret'lar yedeklenen anahtar geri yüklenmeden çözülemez.
