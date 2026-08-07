# 🐛 FAZ-7 Sonrası Sorun & Çözüm Raporu

**Tarih:** 06-07 Ağustos 2026
**Kapsam:** Faz 7 kapanışından sonra, sistemi "webtten tek tıkla deploy" çalışır hale getirirken yaşanan tüm sorunlar.

---

## 📖 Bu Raporu Nasıl Okumalısın?

Her sorun şu bölümlerle anlatılır:

```
┌─────────────────────────────────────────────┐
│  🔴 SORUN N°X:  Sorunun kısa adı            │
├─────────────────────────────────────────────┤
│  🚨 BELİRTİ   → Ne oldu? (ekranda ne gördük)│
│  🔍 TESPİT    → Nasıl buldum?               │
│  🛠️ ÇÖZÜM    → Ne yaptım?                  │
│  📁 ETKİLENEN → Hangi dosya/kod değişti?    │
│  🎨 GÖRSEL    → Benzetme / şema             │
└─────────────────────────────────────────────┘
```

---

## 📊 Hızlı Özet — 10 Sorun

| # | Sorun | Ne Kadar Ciddiydi? | Çözüm Süresi |
|---|-------|-------------------|--------------|
| 1 | Frontend beyaz ekran (Hook hatası) | 🔴 Yüksek — panel açılmıyordu | 10 dk |
| 2 | Namespace çift `build-build-` | 🟠 Orta — isimler bozuluyordu | 5 dk |
| 3 | Veritabanı tablosu yok | 🔴 Yüksek — build kayıtları kayboluyordu | 15 dk |
| 4 | Dispatch 404 (`main.yaml`) | 🔴 Yüksek — build hiç başlamıyordu | 5 dk |
| 5 | Dispatch 422 (`ref_name`) | 🔴 Yüksek — GitHub isteği reddediyordu | 5 dk |
| 6 | ArgoCD 415 (header hatası) | 🟠 Orta — sync çağrısı reddediliyordu | 30 dk |
| 7 | Overlay image adı uyuşmazlığı | 🔴 Yüksek — deploy edilen image değişmiyordu | 15 dk |
| 8 | App create permission denied | 🔴 Yüksek — yeni namespace'ler deploy olamıyordu | 10 dk |
| 9 | Namespace otomatik oluşmuyor | 🟠 Orta — elle oluşturmak gerekiyordu | 10 dk |
| 10 | Overlay push çakışması | 🟠 Orta — push reddediliyordu | 10 dk |

**Ek sorunlar (altyapı):** Disk dolması (16GB → 32GB), runner sürüm uyumsuzluğu (ARM64), backend GITHUB_TOKEN gereksinimi.

---

# 🔴 SORUN 1 — Frontend Beyaz Ekran

## 🚨 Belirti

Web paneli (frontend) açıldığında 1 saniye "yükleniyor" yazıyor, sonra **ekran bembeyaz oluyor**. Konsolda hata:

```
Warning: React has detected a change in the order of Hooks called by BuildStatusPanel
Uncaught Error: Rendered more hooks than during the previous render.
```

## 🔍 Tespit

Konsol hatası bana **"Rules of Hooks"** ihlalini gösterdi. React'in kuralı şudur:

> Hook'lar (`useState`, `useEffect`) asla döngü veya koşul içinde çağrılmaz.

Kodu açtığımda gördüm ki, ben daha önce (K2.15 frontend bağlantısında) `builds.map()` döngüsünün **İÇİNE** bir `useEffect` koymuştum:

```javascript
{builds.map((item) => {
  useEffect(() => {        // ❌ YANLIŞ! Hook döngü içinde
    fetchArgoStatus(ns);
  }, [ns]);
  ...
})}
```

React her render'da hook sayısını sayar — döngü içindeki hook bazen 1 bazen 0 çağrıldığı için sayı değişiyor → "Rendered more hooks" → çökme → beyaz ekran.

## 🛠️ Çözüm

Hook'u döngüden çıkardım. Döngü içindeki `useEffect`'i sildim, yerine **bileşen seviyesinde** tek bir `useEffect` ekledim — `builds` listesi değişince tüm namespace'lerin durumunu topluca çeker:

```javascript
// ✅ Doğru: hook bileşen seviyesinde
useEffect(() => {
  const namespaces = (builds || []).map((b) => b.namespaceName || b.namespace);
  namespaces.forEach((ns) => fetchArgoStatus(ns));
}, [builds, latestBuildTrigger, fetchArgoStatus]);
```

## 📁 Etkilenen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `frontend/src/components/BuildStatusPanel.js` | Hook döngüden çıkarıldı, toplu çekim eklendi |

## 🎨 Görsel — Hook Kuralı

```
❌ YANLIŞ (önceki)              ✅ DOĞRU (sonraki)
┌──────────────────┐            ┌──────────────────┐
│ builds.map(...)  │            │ Bileşen seviyesi │
│   ├─ useEffect ←│❌ döngü içi │   ├─ useEffect ✅│
│   ├─ useEffect ←│❌           │   ├─ useEffect ✅│
│   └─ useEffect ←│❌           │   └─ useEffect ✅│
└──────────────────┘            └──────────────────┘
  Hook sayısı değişiyor           Hook sayısı sabit
  → ÇÖKER                        → ÇALIŞIR
```

---

# 🔴 SORUN 2 — Namespace Çift "build-build-"

## 🚨 Belirti

Yeni build başlatınca namespace adı şöyle oluşuyordu:

```
❌ build-build-build-20260806-134820
❌ build-build-20260806-134538
```

Olması gereken (CONTRACT §2):

```
✅ build-20260806-build
```

## 🔍 Tespit

Backend kodu (`GitHubBuildProvider.cs`) namespace'i şöyle üretiyordu:

```csharp
var namespaceName = $"build-{request.Tag.ToLowerInvariant()}";
```

Tag zaten `BUILD-20260806-...` ile başladığı için `build-` + `build-...` = **çift "build-"**.

## 🛠️ Çözüm

CONTRACT §2'ye uygun format: `build-{yyyyMMdd}-{ticket}`

```csharp
var ticket = request.Tag.Split('-').FirstOrDefault() ?? "K1";
var namespaceName = $"build-{DateTime.UtcNow:yyyyMMdd}-{ticket.ToLowerInvariant()}";
```

## 📁 Etkilenen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `backend/Services/GitHubBuildProvider.cs` | Namespace formatı düzeltildi |

## 🎨 Görsel

```
ESKİ:  "build-" + "BUILD-20260806-134820"  =  build-build-20260806-134820 ❌
YENİ:  "build-" + tarih + "-" + ticket     =  build-20260806-build        ✅
```

---

# 🔴 SORUN 3 — Veritabanı Tablosu Yok

## 🚨 Belirti

Webten deploy başlatınca build çalışıyor ama **panelde hiçbir build görünmüyordu**. API'den bakınca:

```json
[]   // boş liste!
```

Ayrıca backend log'unda sessizce yutulan bir hata vardı:

```
Could not persist build history to PostgreSQL DB. Proceeding.
```

## 🔍 Tespit

1. Önce DB'de tablo var mı diye baktım:

```bash
docker exec datactive_postgres psql -U postgres -d datactive_gitops -c "\dt"
# Çıktı: Did not find any relations.  ← TABLO YOK!
```

2. Sebep: EF Core migration'ı (K1.3'te yazılmış `InitialCreate`) hiç çalıştırılmamıştı. Backend açılırken migration'ı otomatik uygulamıyordu.

## 🛠️ Çözüm

1. **Acil:** Tabloyu elle SQL ile oluşturdum (geçici).
2. **Kalıcı:** `Program.cs`'e açılışta otomatik migration ekledim:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();   // ✅ Açılışta tablolar otomatik oluşur
}
```

## 📁 Etkilenen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `backend/Program.cs` | Açılışta `Migrate()` eklendi |

## 🎨 Görsel

```
ÖNCE:  Backend açılır → migration YOK → tablo YOK → kayıtlar kaybolur
SONRA: Backend açılır → Migrate() → tablo OLUŞUR → kayıtlar yazılır ✅
```

---

# 🔴 SORUN 4 — Dispatch 404 (Yanlış Workflow Adı)

## 🚨 Belirti

Webten deploy deyince GitHub'da **hiçbir build run'ı oluşmuyordu**. Backend log'unda:

```
POST .../actions/workflows/main.yaml/dispatches → 404 Not Found
```

## 🔍 Tespit

Log'daki URL'de `main.yaml` yazıyordu. Ama bizim workflow dosyamızın adı **`build.yml`**! Backend kodu yanlış dosya adını arıyordu:

```csharp
var workflowId = "main.yaml";   // ❌ Böyle bir dosya yok!
```

## 🛠️ Çözüm

```csharp
var workflowId = "build.yml";   // ✅ Gerçek dosya adı
```

## 📁 Etkilenen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `backend/Services/GitHubBuildProvider.cs` | `main.yaml` → `build.yml` |

## 🎨 Görsel

```
Backend: "main.yaml'a istek at" → GitHub: "Böyle dosya yok" (404) ❌
Backend: "build.yml'a istek at" → GitHub: "Tamam, başlatıyorum" (204) ✅
```

---

# 🔴 SORUN 5 — Dispatch 422 (Yanlış Parametre Adı)

## 🚨 Belirti

Workflow adı düzeldikten sonra yeni hata:

```
422 Unprocessable Entity: "ref_name" is not a permitted key. "ref" wasn't supplied.
```

## 🔍 Tespit

GitHub'ın `workflow_dispatch` API'si branch parametresini **`ref`** adıyla bekliyor. Backend kodu **`ref_name`** gönderiyordu:

```csharp
var payload = new
{
    ref_name = "main",   // ❌ GitHub bunu tanımıyor
    inputs = new { ... }
};
```

## 🛠️ Çözüm

```csharp
var payload = new
{
    @ref = "main",       // ✅ GitHub'ın beklediği isim
    inputs = new { ... }
};
```

## 📁 Etkilenen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `backend/Services/GitHubBuildProvider.cs` | `ref_name` → `@ref` |

## 🎨 Görsel

```
GitHub API kuralı:  POST /workflows/build.yml/dispatches
                    { "ref": "main", "inputs": {...} }

Backend önce:       { "ref_name": "main" }  → 422 ❌ (yanlış anahtar)
Backend sonra:      { "ref": "main" }       → 204 ✅ (doğru anahtar)
```

---

# 🟠 SORUN 6 — ArgoCD 415 UnsupportedMediaType

## 🚨 Belirti

ArgoCD'ye sync çağrısı gidiyor ama reddediliyordu:

```
ArgoCD Sync returned status UnsupportedMediaType: Invalid content type
```

## 🔍 Tespit

1. Önce curl ile **aynı isteği elle** denedim → **HTTP 200 döndü!** Yani API doğru çalışıyor, sorun C# tarafında.
2. C# ile curl arasındaki farkı aradım → **header farkı**.

C#'ın `PostAsJsonAsync`'i Content-Type'ı doğru gönderiyor ama ArgoCD "Invalid content type" diyordu. Sorun **çift Accept header** ve content-type'ın net olmamasıydı.

## 🛠️ Çözüm

`PostAsJsonAsync` yerine açık `HttpRequestMessage` kullandım, Content-Type'ı netleştirdim:

```csharp
using var syncRequest = new HttpRequestMessage(HttpMethod.Post, $"api/v1/applications/{appName}/sync")
{
    Content = JsonContent.Create(syncPayload, mediaType: new MediaTypeHeaderValue("application/json"))
};
var response = await _httpClient.SendAsync(syncRequest);
```

Ayrıca çift Accept header'ı kaldırdım (Program.cs'te zaten global Accept vardı, serviste tekrar eklenmişti).

## 📁 Etkilenen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `backend/Services/ArgoCdService.cs` | `PostAsJsonAsync` → açık `HttpRequestMessage` + net Content-Type |
| `backend/Program.cs` | `ARGOCD_INSECURE` ile TLS bypass (test ortamı) |

## 🎨 Görsel

```
curl ile:      Content-Type: application/json  → 200 ✅
C# önce:       (belirsiz/çift header)          → 415 ❌
C# sonra:      Content-Type: application/json  → 200 ✅
```

---

# 🔴 SORUN 7 — Overlay Image Adı Uyuşmazlığı

## 🚨 Belirti

En sinsi sorun! Build çalışıyor, image registry'de, ArgoCD sync oluyor — **ama deployment'daki image değişmiyordu**. Hep `placeholder` kalıyordu:

```
Deployment image: harbor.datactive.net/datateam/datactive.web:placeholder  ← HEP BÖYLE!
```

## 🔍 Tespit

Kustomize'ın nasıl çalıştığını hatırladım:

> Kustomize'ın `images:` bloğu **sadece aynı ada sahip** image'ı değiştirir. Ad farklıysa SESSİZCE hiçbir şey yapmaz.

Karşılaştırma:

```
Base'deki deployment:   image: harbor.datactive.net/datateam/datactive.web:placeholder
Overlay'deki images:    name: datactive-registry:5000/datateam/datactive.web
                          newTag: "BUILD-...152953"

Adlar FARKLI → override ÇALIŞMAZ → placeholder kalır ❌
```

## 🛠️ Çözüm

Base'deki image adresini overlay ile aynı yaptım:

```yaml
# manifests/base/deployment.yaml
image: datactive-registry:5000/datateam/datactive.web:placeholder   # ✅ aynı ad
```

Artık overlay'deki `images:` bloğu eşleşiyor → image güncelleniyor.

## 📁 Etkilenen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `manifests/base/deployment.yaml` | Image adresi test registry'ye eşitlendi |

## 🎨 Görsel

```
Kustomize images çalışma mantığı:

images:
  - name: X          ← overlay'deki ad
    newTag: "v2"

Deployment'daki image: X:placeholder  → X:v2  ✅ (ad aynıysa)
Deployment'daki image: Y:placeholder  → DEĞİŞMEZ ❌ (ad farklıysa!)
```

---

# 🔴 SORUN 8 — App Create Permission Denied

## 🚨 Belirti

Yeni build namespace'i için ArgoCD app'i oluşturulamıyordu:

```
ArgoCD Application creation returned Forbidden:
permission denied: applications, create, default/build-20260806-build
```

## 🔍 Tespit

ArgoCD'de **RBAC** kuralımız var (K2.19 — AppProject). `ci-builder` token'ının yetkisi **`build-project`** projesinde. Ama K1.16'nın create isteğinde:

```csharp
project = "default"   // ❌ ci-builder'ın yetkisi yok!
```

Token `build-project`'e yetkili, istek `default` projesine gidiyor → **permission denied** → app oluşmuyor → deploy olmuyor.

## 🛠️ Çözüm

```csharp
project = "build-project"   // ✅ ci-builder'ın yetkili olduğu proje
```

## 📁 Etkilenen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `backend/Services/ArgoCdService.cs` | `project = "default"` → `"build-project"` |

## 🎨 Görsel

```
ci-builder'ın kapı kartı (RBAC):  sadece build-project'e girebilir

İstek önce:  "default projesine app aç"   → Güvenlik: YASAK ❌
İstek sonra: "build-project'e app aç"     → Güvenlik: İZİN ✅
```

---

# 🟠 SORUN 9 — Namespace Otomatik Oluşmuyor

## 🚨 Belirti

App oluşuyor ama sync'te:

```
namespaces "build-20260807-build" not found
```

Namespace hiç yoktu → sync başarısız.

## 🔍 Tespit

Backend akışı overlay + app oluşturuyor ama **namespace'i hiçbir yerde oluşturmuyordu**. Daha önce (K2.13 scriptinde) namespace'i elle `kubectl create namespace` ile yapıyorduk — backend akışında bu adım yoktu.

## 🛠️ Çözüm

ArgoCD'nin **`CreateNamespace` sync option**'ını ekledim — ArgoCD sync sırasında namespace'i kendisi oluşturur:

```csharp
syncPolicy = new
{
    automated = new { prune = true, selfHeal = true },
    syncOptions = new[] { "CreateNamespace=true" }
};
```

Ayrıca sync payload'ına da ekledim:

```csharp
var syncPayload = new { prune = true, dryRun = false, syncOptions = new[] { "CreateNamespace=true" } };
```

## 📁 Etkilenen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `backend/Services/ArgoCdService.cs` | CreateNamespace sync option |

## 🎨 Görsel

```
ÖNCE:  Backend → app oluştur → sync → "namespace yok!" ❌
SONRA: Backend → app oluştur → sync → ArgoCD namespace'i de oluşturur ✅
```

---

# 🟠 SORUN 10 — Overlay Push Çakışması

## 🚨 Belirti

Backend overlay'i commit ediyor ama push edemiyordu:

```
git push failed: ! [rejected] main -> main (fetch first)
```

## 🔍 Tespit

GitOpsOverlayService **push öncesi pull yapmıyordu**. Ben (veya başka biri) GitHub'a yeni commit atınca, backend'in lokal clone'u eski kalıyor → push reddediliyor ("fetch first").

## 🛠️ Çözüm

Push öncesine `git pull --rebase` ekledim:

```csharp
await RunProcessAsync("git", "pull --rebase", repoRoot);   // ✅ önce güncelle
var pushResult = await RunProcessAsync("git", "push", repoRoot);
```

## 📁 Etkilenen Dosyalar

| Dosya | Değişiklik |
|-------|-----------|
| `backend/Services/GitOpsOverlayService.cs` | Push öncesi `pull --rebase` |

## 🎨 Görsel

```
ÖNCE:  commit → push → "remote'da yeni iş var, reddedildi" ❌
SONRA: commit → pull --rebase (güncelle) → push ✅
```

---

# 🟡 ALTYAPI SORUNLARI (Bonus)

## Sorun A — Disk Dolması

**Belirti:** Runner pod'ları Pending'de takılıyor, node'larda `disk-pressure` taint'i.

**Tespit:** k3d cluster'ın diski 16GB'dı. Build image'ları + buildkit cache'i doldurdu.

**Çözüm:**
1. `crictl rmi --prune` ile eski image'ları temizledim
2. Docker ayarlarından disk 32GB'a çıkarıldı (sen)
3. Buildkitd'ye `gc = true` + `gcKeepStorage = 2GB` — cache 2GB'da sabit kalır

## Sorun B — Runner ARM64 Uyumsuzluğu

**Belirti:** Runner "Listening for Jobs" diyor ama GitHub broker'a 403 alıyor.

**Tespit:** Mac Apple Silicon (ARM64) — eski runner sürümü (2.322.0) ARM64 broker'ı desteklemiyordu.

**Çözüm:** Runner image `2.322.0` → `2.336.0` (en güncel).

## Sorun C — Backend GITHUB_TOKEN Gereksinimi

**Belirti:** Webten deploy → dispatch sessizce 401.

**Tespit:** Backend `GITHUB_TOKEN` env'ini okuyor; env yoksa token gönderilmiyor.

**Çözüm:** DEV_ENV.md'ye not eklendi + test için env ile başlatma.

---

## 🏁 SONUÇ — Sistem Artık Nasıl Çalışıyor?

```
Webten "Deploy'u başlat" (tek tık)
    │
    ▼
1️⃣ Backend tag üretir + GitHub'a dispatch atar
    │
    ▼
2️⃣ GitHub Actions build eder (runner + buildkit)
    │
    ▼
3️⃣ Image registry'ye push edilir
    │
    ▼
4️⃣ Backend overlay üretir + commit + push (pull --rebase)
    │
    ▼
5️⃣ Backend ArgoCD'ye app oluştur/upsert der (build-project)
    │
    ▼
6️⃣ ArgoCD namespace'i otomatik oluşturur + sync eder
    │
    ▼
7️⃣ Deployment güncellenir → pod YENİ içerikle çalışır ✅
```

**Artık elle yapılan hiçbir şey yok.** GitHub'a commit at → webtten deploy → pod kendiliğinden güncellenir.

---

## 📚 Öğrenilen Dersler

| Ders | Açıklama |
|------|----------|
| **Kustomize ad eşleşmesi** | `images:` override'ı ad aynıysa çalışır — ad farkı = sessiz hata |
| **React Hook kuralları** | Hook'lar asla döngü/koşul içinde çağrılmaz |
| **GitHub API isimleri** | `ref` vs `ref_name` — API dokümanına sadık kal |
| **RBAC projesi** | ArgoCD'de her istek doğru projeye gitmeli |
| **Push öncesi pull** | Otomatik git işlemlerinde `pull --rebase` şart |
| **Test = canlı simülasyon** | Her sorun test ortamında yakalandı — production'a temiz gitti |
