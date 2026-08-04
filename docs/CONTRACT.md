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

## 7. ArgoCD API Erişim Bilgisi

**Durum: ⚠️ FAZ 5'TE DOLDURULACAK**

```yaml
argocd_api:
  endpoint: ""
  token: ""
  rbac: ""
  notes: "Faz 5'te K2.14 görevi ile doldurulacak"
```

**Açıklama:** ArgoCD ile iletişim için gerekli bilgiler burada olacak. Şimdilik boş.

---

## Değişiklik Kuralları

- Bu dokümandaki değişiklikler PR ile yapılır.
- Her iki ekip (K1, K2) onaylamadan merge edilmez.
- "Şimdilik şöyle yapalım" istisnası YOKTUR.
