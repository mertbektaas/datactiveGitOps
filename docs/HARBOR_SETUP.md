# Harbor Push + Regcred — Production Rehberi

Build edilen image'ların Harbor'a push edilmesi ve cluster'ın bu image'ları çekebilmesi için gerekli kurulum.

## Ön Koşullar

- Harbor kurulu ve erişilebilir: `harbor.datactive.net`
- Harbor'da proje mevcut: `datateam`
- Cluster admin yetkisi

---

## 1. Harbor Robot Hesabı Oluştur

Robot hesabı: sadece push/read yetkili, insan hesabı olmayan otomatik kimlik.

1. Harbor UI → `datateam` projesi → **Robot Accounts** → **New Robot Account**
2. Ayarlar:
   - Name: `gitops-builder`
   - Permission: **Push + Pull** (yalnızca bu proje)
   - Expiration: uzun süre (veya uygun politikaya göre)
3. Oluşturulan **token'ı** güvenli yere kaydet (bir daha görünmez)

> ⚠️ Robot token'ı repo'ya veya koda yazılmaz. Secret'ta saklanır.

---

## 2. Buildkitd Push Auth (buildkitd.toml)

`manifests/arc/buildkitd.yaml` içindeki ConfigMap'e production registry bilgisi:

```toml
[registry."harbor.datactive.net"]
  http = false
  insecure = false

[registry."harbor.datactive.net".auth]
  username = "robot$gitops-builder"
  password = "<ROBOT_TOKEN>"
```

> ⚠️ Şifre düz metin değil — **SealedSecret'a dönüştürülür** (K2.12) ve buildkitd'ye secret volume mount edilir.

---

## 3. regcred Secret Oluştur

Cluster'ın Harbor'dan image çekmesi için:

```bash
kubectl create secret docker-registry regcred \
  --namespace <hedef-namespace> \
  --docker-server=harbor.datactive.net \
  --docker-username='robot$gitops-builder' \
  --docker-password='<ROBOT_TOKEN>' \
  --dry-run=client -o yaml | kubectl apply -f -
```

> `--dry-run` + apply: secret'ın repo'ya manifest olarak eklenebilmesi için (veya SealedSecret'a dönüştürülür, K2.12).

---

## 4. Deployment'da imagePullSecrets

K2.1'de hazırlanan `manifests/base/deployment.yaml` zaten içeriyor:

```yaml
spec:
  template:
    spec:
      imagePullSecrets:
        - name: regcred
```

Namespace overlay'lerde `regcred` secret'ının varlığı doğrulanmalı.

---

## 5. Doğrulama

```bash
# Image registry'de mi?
curl -s -u "robot$gitops-builder:<TOKEN>" \
  "https://harbor.datactive.net/api/v2.0/projects/datateam/repositories/datactive.web/artifacts?page_size=1"

# Cluster image'ı çekebiliyor mu? (regcred ile)
kubectl run pull-check --rm -i --restart=Never \
  --image=harbor.datactive.net/datateam/datactive.web:<tag> \
  --overrides='{"spec":{"imagePullSecrets":[{"name":"regcred"}]}}' \
  --command -- python3 -c "print('OK')"
```

---

## 6. Rollback / Güvenlik

- **Robot token sızarsa:** Harbor'da robot hesabını sil → yeni hesap + yeni regcred
- **Buildkitd config'te token:** SealedSecret (K2.12) ile şifrelenir, düz metin repo'da tutulmaz
- **Push fail durumunda:** buildkitd log'ları + token geçerliliği kontrol edilir (`401 Unauthorized` = token sorunu)
