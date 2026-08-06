# Sealed Secrets — Production Key Yönetimi & Rotation

K2.18. Secret'ların üretimde güvenli yönetimi, anahtar yedekleme ve şifre değişimi (rotation) prosedürü.

---

## 1. Anahtar Mimarisi (Özet)

```
┌─ Public Key (herkese açık) ────────┐   ┌─ Private Key (gizli) ──────────┐
│  Şifreleme yapar                    │   │  Çözme yapar                    │
│  Repo'da: docs/cert/…              │   │  Cluster'da: sealed-secrets-key  │
│  kubeseal --cert ile kullanılır    │   │  SADECE controller'da           │
└─────────────────────────────────────┘   └─────────────────────────────────┘
```

- **Public key kaybı** → yeni şifreleme yapılamaz (mevcutlar çözülür, sorun yok)
- **Private key kaybı** → TÜM SealedSecret'lar çözülemez (FELAKET)

---

## 2. Public Key Yedekleme

```bash
# Controller'dan public key'i al (kurulum sonrası)
kubeseal --fetch-cert \
  --controller-name sealed-secrets-controller \
  --controller-namespace kube-system \
  > docs/cert/sealed-secrets-cert.pem

# Git'e commit et (public key herkese açık — güvenli)
git add docs/cert/sealed-secrets-cert.pem
git commit -m "chore: sealed-secrets public key"
git push
```

> Public key repo'da durur — şifreleme yapacak herkes (K1.10, CI) buradan alır.

---

## 3. Private Key Yedekleme (KRİTİK)

```bash
# Private key'i dışa al
kubectl get secret sealed-secrets-key -n kube-system \
  -o jsonpath='{.data.tls\.key}' | base64 -d > sealed-secrets-private-key.pem

# GÜVENLİ YERE KAYDET — ASLA REPO'YA KOYMA
#   ✅ 1Password / Vault / kasa / offline depolama
#   ❌ GitHub, e-posta, sohbet
```

**Yedekleme kuralı:**
- Cluster yeniden kurulursa → yedek private key geri yüklenir, eski SealedSecret'lar çözülür
- Yedek yoksa → tüm şifreler yeniden şifrelenmek zorunda kalınır (kesinti)

---

## 4. Rotation — DB Şifresi Değiştiğinde

**Senaryo:** PostgreSQL şifresi değişti → uygulamanın DB bağlantısı güncellenmeli.

### Adım Adım:

```bash
# 1. Düz secret'ı yeni değerle güncelle (placeholder dosyası)
#    manifests/base/datactive-secret.yaml içindeki DefaultConnection

# 2. Yeniden şifrele (K2.12 scripti — üzerine yazar)
bash scripts/seal-secret.sh \
  manifests/base/datactive-secret.yaml \
  docs/cert/sealed-secrets-cert.pem

# 3. Değişikliği commit + push et (GitOps)
git add manifests/base/datactive-sealed-secret.yaml
git commit -m "chore: rotate DB password"
git push

# 4. ArgoCD değişikliği algılar → otomatik sync
#    (veya elle: argocd app sync <app>)

# 5. Doğrula — pod'lar yeni secret'ı aldı mı?
kubectl get pods -n <namespace> -o wide
kubectl get secret datactive-secret -n <namespace> \
  -o jsonpath='{.data.DefaultConnection}' | base64 -d | grep -c "yeni-şifre" 
```

**Önemli:** SealedSecret değişince controller yeni Secret oluşturur ama **çalışan pod'lar eski env'i tutar** → Deployment restart gerekir (ArgoCD sync bunu tetikler, image değişmezse rollout restart manuel).

---

## 5. Rotation — Controller Key'i Değiştiğinde (Nadir)

**Senaryo:** Private key sızdı → controller key'i yenileme.

```bash
# 1. Controller'ı sil → yeni key üretilir
helm uninstall sealed-secrets --namespace kube-system
helm install sealed-secrets sealed-secrets/sealed-secrets \
  --namespace kube-system \
  --values manifests/sealed-secrets/values.yaml

# 2. TÜM SealedSecret'lar yeniden şifrelenmeli (yeni public key ile)
#    Çünkü eski private key gitti → eski şifreli veriler çözülemez
bash scripts/seal-secret.sh \
  manifests/base/datactive-secret.yaml \
  docs/cert/sealed-secrets-cert.pem   # ← YENİ cert'i önce al (adım 4'teki fetch)

# 3. Yeni public key'i repo'ya commit et
```

> ⚠️ Bu işlem tüm secret'ları etkiler — sadece sızıntı durumunda yapılır.

---

## 6. Secret'lara Kim Erişebilir? (k8s RBAC)

| Rol | Erişim |
|-----|--------|
| **Cluster admin (boss)** | Her şey — sealed-secrets-key dahil |
| **ArgoCD (deploy)** | Secret okuyabilir (deployment için) |
| **Uygulama pod'u** | Sadece KENDİ namespace'indeki kendi secret'ı |
| **Geliştiriciler** | SealedSecret oluşturabilir (public key ile), düz Secret'a erişemez |
| **ci-builder (ArgoCD token)** | Secret'lara ERİŞEMEZ (build-project resourceWhitelist'te sadece Secret objesi oluşturabilir, okuyamaz) |

```bash
# Kontrol: bir kullanıcı/token secret okuyabilir mi?
kubectl auth can-i get secrets --as=system:serviceaccount:<ns>:<sa> -n <ns>
```

---

## 7. DevOps ile Birlikte Yapılacaklar (Gerçek Secret Dönüşümü)

Boss/DevOps ortamında:

```bash
# 1. Gerçek şifrelerle düz secret oluştur (LOKAL — repo'ya pushlamadan)
cat > /tmp/real-secret.yaml <<EOF
apiVersion: v1
kind: Secret
metadata:
  name: datactive-secret
type: Opaque
stringData:
  DefaultConnection: "Host=...;Password=<GERÇEK>;..."
EOF

# 2. Şifrele (public key ile)
kubeseal --format yaml --cert docs/cert/sealed-secrets-cert.pem \
  < /tmp/real-secret.yaml > manifests/base/datactive-sealed-secret.yaml

# 3. Düz secret'ı SİL (repo'ya girmez) + sealed'ı commit et
rm /tmp/real-secret.yaml
git add manifests/base/datactive-sealed-secret.yaml
git commit -m "chore: seal real datactive secret"
git push
```

---

## 8. Felaket Senaryoları

| Senaryo | Etki | Kurtarma |
|---------|------|----------|
| Public key kaybı | Yeni şifreleme yapılamaz | Controller'dan yeniden fetch (adım 2) |
| Private key kaybı | TÜM secret'lar çözülemez | Yedekten geri yükle (adım 3) — yedek yoksa hepsi yeniden |
| Cluster silindi | Secret'lar kaybolur | Yedek private key + repo'daki SealedSecret'lar → yeni cluster'da çözülür |
| Token sızıntısı | Yetkisiz erişim | Controller key rotation (adım 5) |
