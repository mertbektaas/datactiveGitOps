# Sealed Secrets — Kurulum Rehberi

Bu rehber, Sealed Secrets controller'ının k3s cluster'ına kurulumunu ve kullanıma hazır hale getirilmesini adım adım açıklar.

> ⚠️ **Bu kurulum, cluster yöneticisi (boss/DevOps) tarafından yapılır.** Geliştiricinin cluster'a erişimi yoktur.

---

## Ön Koşullar

- k3s cluster çalışır durumda
- `kubectl` kurulu ve cluster'a bağlı
- `helm` kurulu (v3+)
- Cluster üzerinde admin yetkisi

---

## 1. Helm Kurulumu (yoksa)

```bash
# macOS
brew install helm

# Linux
curl -fsSL https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash

# Doğrula
helm version
```

---

## 2. Sealed Secrets Repo'sunu Tanıt

```bash
helm repo add sealed-secrets https://bitnami-labs.github.io/sealed-secrets
helm repo update
```

Bu komut, Helm'e "Sealed Secrets paketlerini bu mağazadan indir" der.

---

## 3. Controller'ı Kur

Repo içindeki hazır değerler dosyasıyla kurulum:

```bash
# Repo'da: manifests/sealed-secrets/values.yaml
helm install sealed-secrets sealed-secrets/sealed-secrets \
  --namespace kube-system \
  --values manifests/sealed-secrets/values.yaml
```

**values.yaml içeriği:**
```yaml
# manifests/sealed-secrets/values.yaml
fullnameOverride: sealed-secrets-controller

controller:
  service:
    type: ClusterIP
```

**Yapılanlar:**
- `kube-system` namespace'ine kurulur (cluster çekirdek bileşenleriyle aynı yerde)
- `fullnameOverride` ile controller adı `sealed-secrets-controller` olur (net isimlendirme)

---

## 4. kubeseal CLI Kurulumu

kubeseal, geliştiricinin şifreleme yaparken kullanacağı komut satırı aracıdır.

```bash
# macOS
brew install kubeseal

# Linux (indirme)
curl -Lo kubeseal.tar.gz \
  https://github.com/bitnami-labs/sealed-secrets/releases/download/v0.27.1/kubeseal-0.27.1-linux-amd64.tar.gz
tar -xzf kubeseal.tar.gz kubeseal
sudo install -m 755 kubeseal /usr/local/bin/kubeseal

# Doğrula
kubeseal --version
```

---

## 5. Public Key'i Dışa Al ve Repo'ya Koy

Public key, geliştiricilerin şifreleme yapması için gereklidir. **Repo'ya konabilir** (açık anahtar).

```bash
# Public key'i al
kubeseal --fetch-cert \
  --controller-name sealed-secrets-controller \
  --controller-namespace kube-system \
  > docs/cert/sealed-secrets-cert.pem

# Doğrula
cat docs/cert/sealed-secrets-cert.pem
```

> ⚠️ Bu dosya `docs/cert/` altına kaydedilir ve GitHub'a pushlanır. Böylece tüm geliştiriciler bu anahtarla şifreleme yapabilir.

---

## 6. ⚠️ KRİTİK: Private Key Yedekleme

Private key, şifreleri çözmeye yarayan **gizli anahtardır**. Kaybolursa:
- **Tüm mevcut şifreli veriler çözülemez** (felaket!)
- Cluster yeniden kurulursa aynı anahtar olmadan eski şifreler okunamaz

```bash
# Controller'dan private key'i dışa al
kubectl get secret -n kube-system sealed-secrets-key \
  -o jsonpath='{.data.tls\.key}' | base64 -d > sealed-secrets-private-key.pem

# Bu dosyayı GÜVENLİ bir yere sakla (1Password, kasada, offline)
# ⚠️ ASLA GitHub'a pushlama, ASLA e-posta ile gönderme!
```

**Yedekleme Kuralları:**
- ❌ Private key repo'ya konmaz
- ❌ Private key public ortama atılmaz
- ✅ 1Password / güvenli kasa / offline depolama

---

## 7. Kurulumu Doğrula

```bash
# Controller pod'unun çalıştığını kontrol et
kubectl get pods -n kube-system | grep sealed

# Beklenen çıktı:
# sealed-secrets-controller-xxx Running

# Controller log hatasız mı?
kubectl logs -n kube-system -l app=sealed-secrets

# Public key dönüyor mu?
kubeseal --fetch-cert \
  --controller-name sealed-secrets-controller \
  --controller-namespace kube-system > /dev/null && echo "OK"
```

---

## 8. Çalışma Akışı (Sonrası)

Kurulum tamamlandıktan sonra geliştirici akışı:

```
1. Geliştirici: kubeseal ile şifrele
   kubectl create secret generic testvalue --dry-run=client -o yaml | kubeseal --format yaml > sealedsecret.yaml

2. SealedSecret dosyası GitHub'a pushlanır (şifreli, güvenli)

3. Controller: şifreli dosyayı görür → private key ile çözer → gerçek Secret oluşturur
```

---

## Rollback (Geri Alma)

```bash
# Controller'ı tamamen sil
helm uninstall sealed-secrets --namespace kube-system
```

> ⚠️ Not: Rollback sonrası private key kaybolursa mevcut SealedSecret'lar okunamaz. Private key'i mutlaka sakla.

---

## Özet Kontrol Listesi

- [ ] Helm kuruldu
- [ ] sealed-secrets repo tanıtıldı
- [ ] Controller `kube-system` içinde çalışıyor
- [ ] kubeseal CLI kuruldu
- [ ] Public key `docs/cert/sealed-secrets-cert.pem` olarak repo'da
- [ ] Private key güvenli yerde yedeklendi
- [ ] Controller log hatasız
- [ ] `kubeseal --fetch-cert` public key dönüyor
