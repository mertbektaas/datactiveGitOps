# Geliştirme Ortamı Kurulumu

Bu doküman, K1 (frontend/backend) ve K2 (CI/CD) ekiplerinin ortak geliştirme ortamı bilgilerini içerir.

> ⚠️ **ÖNEMLİ**: Bu dokümanda gerçek şifre/token YOKTUR. Tüm credential'lar `~/.env` veya 1Password'de saklanır.

---

## Adım Adım Kurulum

### 1. Cluster'a Bağlanma (k3s)

**Kim ihtiyaç duyar:** K2 (CI/CD manifestleri test etmek için)

**Gereksinimler:**
- `kubectl` CLI kurulu olmalı
- kubeconfig dosyası gerekli

**Adımlar:**
```bash
# kubeconfig dosyasını yerleştir
cp ~/path/to/kubeconfig ~/.kube/config

# Bağlantıyı test et
kubectl cluster-info
kubectl get nodes
```

**Referans:** 1Password → "k3s-kubeconfig" veya DevOps'tan iste

---

### 2. Harbor'a Giriş Yapma

**Kim ihtiyaç duyar:** K2 (image push/pull için)

**Gereksinimler:**
- Docker kurulu olmalı
- Harbor credential'ları

**Adımlar:**
```bash
# Harbor'a giriş yap
docker login harbor.datactive.net

# Kullanıcı adı: 1Password'den al
# Şifre: 1Password'den al
```

**Adres:** `harbor.datactive.net`  
**Proje:** `datateam`  
**Referans:** 1Password → "harbor-credentials"

---

### 3. GitHub PAT Oluşturma

**Kim ihtiyaç duyar:** K1 (GitHub API erişimi için)

**Gereksinimler:**
- GitHub hesabı
- PAT (Personal Access Token)

**Adımlar:**
1. GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)
2. "Generate new token" tıkla
3. Aşağıdaki yetkileri seç:
   - `repo` (tam erişim)
   - `workflow` (GitHub Actions tetikleme)
4. Token oluştur ve `~/.env` dosyasına kaydet

**Yetkiler:**
```bash
# ~/.env dosyasına ekle
GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx
GITHUB_REPO=mertbektaas/datactiveGitOps
```

**Referans:** 1Password → "github-pat-template"

---

## Özet Tablo

| Kaynak | Adres/Endpoint | Credential Nerede | Kim Kullanır |
|--------|----------------|-------------------|--------------|
| k3s Cluster | `~/.kube/config` | 1Password / DevOps | K2 |
| Harbor | `harbor.datactive.net` | 1Password | K2 |
| GitHub PAT | — | `~/.env` | K1 |
| Sealed Secrets Cert | Cluster içinde | Controller'dan çek | K2 |
| ArgoCD API | `http://localhost:18080` (test) | `ARGOCD_TOKEN` (ci-builder) | K2 / K1.16 |

---

## Ortam Değişkenleri

Her geliştirici `~/.env` dosyasında aşağıdaki değişkenleri tutar:

```bash
# GitHub
GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx
GITHUB_REPO=mertbektaas/datactiveGitOps

# Harbor (opsiyonel, docker login ile de yapılır)
HARBOR_USER=username
HARBOR_PASS=********

# Cluster (kubeconfig yolu)
KUBECONFIG=~/.kube/config
```

---

## Kontrol Listesi

Kurulum sonrası kontrol:

- [ ] `kubectl cluster-info` çalışıyor
- [ ] `docker login harbor.datactive.net` başarılı
- [ ] `~/.env` dosyasında `GITHUB_TOKEN` var
- [ ] `.gitignore`'da `*.env` var (gerçek şifreler commit edilmez)

---

## Sorun Giderme

**kubectl bağlanamıyor:**
- kubeconfig dosyasını kontrol et
- DevOps'tan yeni kubeconfig iste

**Harbor login başarısız:**
- 1Password'den doğru credential'ları al
- Network/VPN kontrolü yap

**GitHub API 401:**
- PAT süresi dolmuş olabilir → yeni oluştur
- Yetkileri kontrol et (`repo`, `workflow`)
