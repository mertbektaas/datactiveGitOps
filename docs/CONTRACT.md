# CONTRACT.md — Entegrasyon Sözleşmesi

Bu doküman, K1 (frontend/backend) ve K2 (workflows/manifests) ekiplerinin ortak çalışma sözleşmesidir. Tüm entegrasyon noktaları burada tanımlanır.

---

## 1. Tag Formatı

Her build'e benzersiz bir tag verilir.

```yaml
tag_format:
  pattern: "{ticket}-{yyyyMMdd-HHmmss}"
  example: "K1-5-20260804-143022"
  rules:
    - ticket: Issue/task numarası (örn: K1-5, C0-2)
    - yyyyMMdd: Tarih (Yıl-Ay-Gün)
    - HHmmss: Saat (Saat-Dakika-Saniye)
```

**Açıklama:** Tag, hangi ticket'tan ve ne zaman build edildiğini gösterir. Format sabittir, değiştirilemez.

---

## 2. Namespace Kuralı

Her build için Kubernetes cluster'ında geçici bir namespace açılır.

```yaml
namespace_format:
  pattern: "build-{yyyyMMdd}-{ticket}"
  example: "build-20260804-K1-5"
  rules:
    - yyyyMMdd: Build tarihi
    - ticket: Issue/task numarası
    - Ömür: Deploy tamamlandıktan sonra silinir (Faz 6'da tanımlanır)
```

**Açıklama:** Her build izole bir ortamda çalışır. Namespace ismi tarih ve ticket'tan oluşur.

---

## 3. Workflow Input Parametreleri

GitHub Actions workflow'una dışarıdan gönderilen parametreler.

```yaml
workflow_inputs:
  branch_web:
    type: string
    description: "Web uygulaması branch adı"
    required: true
    example: "feat/K1-5-auth"
  
  branch_server:
    type: string
    description: "Server uygulaması branch adı"
    required: true
    example: "feat/K1-5-api"
  
  schema:
    type: string
    description: "DB schema adı"
    required: true
    example: "schema_k1_5"
  
  tag:
    type: string
    description: "Build tag'i (Tag Formatı'na uygun)"
    required: true
    example: "K1-5-20260804-143022"
```

**Açıklama:** Workflow tetiklendiğinde bu 4 parametre gönderilir. İsimler sabittir.

---

## 4. Overlay Klasör Formatı

Her build için Kustomize overlay klasörü oluşturulur.

```yaml
overlay_structure:
  path: "manifests/overlays/{namespace}"
  files:
    - kustomization.yaml
    - deployment-patch.yaml
    - service-patch.yaml
  
  kustomization_yaml:
    apiVersion: kustomize.config.k8s.io/v1beta1
    kind: Kustomization
    namespace: "{namespace}"
    resources:
      - ../../base
    images:
      - name: harbor.example.com/datateam-web
        newTag: "{tag}"
      - name: harbor.example.com/datateam-server
        newTag: "{tag}"
```

**Açıklama:** Her build için `manifests/overlays/build-{tarih}-{ticket}/` altında overlay oluşur. `kustomization.yaml` base'i referans eder ve image tag'lerini günceller.

---

## 5. Branch Eşleştirme

**Durum: ✅ KARAR VERİLDİ**

```yaml
branch_matching:
  method: "manuel"
  description: |
    Kullanıcı web ve server branch'lerini manuel olarak seçer.
    Otomatik eşleştirme YOK.
    UI'da iki ayrı dropdown: biri web branch'leri, diğeri server branch'leri gösterir.
  ui_behavior:
    web_branches: "GET /api/branches?type=web"
    server_branches: "GET /api/branches?type=server"
    user_selects: "Her iki dropdown'dan birer branch seçer"
```

**Açıklama:** Web ve server branch'leri bağımsız seçilir. Otomatik pairing yoktur.

---

## 6. GitHub Değişiklikleri Algılama

**Durum: ✅ KARAR VERİLDİ**

```yaml
change_detection:
  method: "polling"
  trigger: "manuel_buton"
  description: |
    Kullanıcı menüden "Değişiklikleri Kontrol Et" butonuna tıklar.
    Backend GitHub API'yi çağırır, yeni commit/varışları kontrol eder.
    Webhook YOK, otomatik bildirim YOK.
  api_endpoint: "GET /api/changes/check"
  response:
    has_changes: boolean
    new_commits: array
```

**Açıklama:** Değişiklik kontrolü kullanıcı tetiklemesiyle yapılır. Polling ile GitHub API'ye sorulur.

---

## 7. GitHub PAT Yetki Listesi

**Durum: ✅ KARAR VERİLDİ**

```yaml
github_pat:
  type: fine-grained
  repos:
    - datateam-web
    - datateam-core.server
  permissions:
    metadata: read
    contents: read
    actions: write
  expiration: "30 gün (yenilenir)"
  storage: "github secret (PAT_TOKEN)"
  lifecycle: "Geçici — Azure DevOps geçişinde iptal edilecek"
  principle: "En az yetki; yalnızca gerekli 2 repo, yalnızca gerekli 3 yetki"
```

**Açıklama:**
- **Tür:** Fine-grained PAT (repo seçimli)
- **Erişim:** Sadece `datateam-web` ve `datateam-core.server`
- **Yetkiler:**
  - `metadata: read` — Repo bilgileri
  - `contents: read` — Branch listeleme
  - `actions: write` — workflow_dispatch tetikleme
- **Saklama:** GitHub repo secret `PAT_TOKEN` (kodda token YOK)
- **Süre:** 30 gün, düzenli yenilenir
- **Ömür:** Geçici — GitHub Actions Azure DevOps ile değiştirileceğinden PAT iptal edilir

---

## 8. Secret Yönetimi (DB Bağlantısı)

**Durum: ✅ KARAR VERİLDİ**

```yaml
db_secret:
  decision: "Tek DB — tüm build'ler aynı DefaultConnection kullanır"
  location: "base/datactive-secret.yaml (tek kopya)"
  isolation: "Schema seviyesinde sağlanır (ConfigMap per-overlay)"
  sealed: "SealedSecret — şifreli hali git'te durur (K2.12)"
  rationale: |
    - Boss'un altyapısı tek PostgreSQL kullanır
    - Her build farklı schema'da çalışır (veri izolasyonu zaten var)
    - Secret tek yerde → tek şifre yönetimi
    - Schema env'leri overlay'de (per-build), connection string base'de
```

**Açıklama:**
- Her build kendi namespace'inde çalışır ama **aynı DB'ye bağlanır**
- İzolasyon **schema** ile sağlanır (her build farklı schema → farklı veri)
- `DefaultConnection` (secret) → **base'de** tek kopya
- `DQLSchema`, `ORACLEDataSchema` vb. (config) → **overlay'de** build'e özel
- SealedSecret, git'te şifreli durur; controller çözer (K2.3/K2.12)

---

## 9. ArgoCD API Erişim Bilgisi

**Durum: ✅ DOLDURULDU (test ortamında doğrulandı)**

```yaml
argocd_api:
  # Test ortamı (k3d): port-forward 18080
  # Production: boss sağlayacak (örn: https://argocd.datactive.net)
  endpoint: "http://localhost:18080"
  
  # Auth: ArgoCD apiKey (ci-builder hesabı) — Bearer token
  # Production'da boss'un sağladığı token kullanılır
  auth:
    type: "bearer-token"
    token_ref: "ARGOCD_TOKEN (env) / k8s secret (K1.16)"
    rbac: "build-* uygulamaları: get/create/sync (K2.14)"
  
  # Örnek istekler (K1.16'nın kullanacağı)
  endpoints:
    app_create: |
      POST /api/v1/applications
      { "metadata": { "name": "<namespace>" },
        "spec": {
          "project": "default",
          "source": { "repoURL": "<repo>", "path": "manifests/overlays/<namespace>" },
          "destination": { "server": "https://kubernetes.default.svc", "namespace": "<namespace>" } } }
    app_sync: |
      POST /api/v1/applications/<namespace>/sync
    app_status: |
      GET  /api/v1/applications/<namespace>?refresh=normal
      → .status.sync.status (Synced/OutOfSync) + .status.health.status
  
  notes: |
    - CLI karşılığı: scripts/argocd-app.sh (K2.13)
    - Test ortamı token'ı geçicidir; production'da boss sağlar
    - K1.16 web app bu endpoint'leri kullanır
```

**Açıklama:** K1.16, ArgoCD API'yi yukarıdaki endpoint'lerle çağırır. Auth: `Authorization: Bearer <token>`. Test ortamında doğrulandı (app create + sync + status).

---

## 10. Auth Kararı (Panel Erişimi)

**Durum: ✅ KARAR VERİLDİ**

```yaml
auth_decision:
  panel_access: "internal (şirket ağı / VPN)"
  login_required: "EVET — 2FA ile login (K1.19 uygulayacak)"
  external_access: "YOK — webhook yok (C0.5 kararı, polling)"
  api_auth: "ArgoCD token zaten korumalı (K2.14); GitHub PAT zaten korumalı (C2.1)"
  rationale: |
    - Deploy tetikleme production'ı değiştirir → yetkisiz kullanım riskli
    - 2FA: şifre çalınsa bile ikinci faktör korur
    - Dışa açıklık yok (polling, webhook yok)
```

**Açıklama:** Panel şirket içi kullanılır (dışa açık değil). Giriş **2FA ile login** ile korunur — deploy tetikleme production'ı değiştirdiği için yalnızca doğrulanmış ekip üyeleri erişebilir (K1.19).

---

## Değişiklik Kuralları

- Bu dokümandaki değişiklikler PR ile yapılır.
- Her iki ekip (K1, K2) onaylamadan merge edilmez.
- "Şimdilik şöyle yapalım" istisnası YOKTUR.
