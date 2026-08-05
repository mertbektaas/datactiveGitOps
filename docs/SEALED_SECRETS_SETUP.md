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

## Rollback

```bash
helm uninstall sealed-secrets --namespace kube-system
```

Rollback sonrasında controller yeni bir private key üretir; mevcut SealedSecret'lar yedeklenen anahtar geri yüklenmeden çözülemez.
