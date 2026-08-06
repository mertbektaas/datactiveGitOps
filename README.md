# datactiveGitOps

Datactive ürününün (datateam-web + datateam-core.server) otomatik build & deploy sistemi.

## Mimari

```
Kullanıcı (Deploy Paneli)
    │ branch_web + branch_server + schema seçer
    ▼
Backend (.NET 8) — POST /api/builds
    │ workflow_dispatch (GitHub) veya Azure Pipeline (K2.17)
    ▼
Build (GitHub Actions / Azure)
    │ git clone ×2 → buildctl → buildkitd (cluster'da daemon)
    ▼
Registry (Harbor — production / k3d — test)
    │ image push
    ▼
Overlay üretimi (K1.10) → commit → push (manifests/overlays/build-*)
    │
    ▼
ArgoCD — Application izler + sync
    │
    ▼
Kubernetes cluster — build-{tarih}-{ticket} namespace (izole deploy)
```

Her build için: yeni namespace + Kustomize overlay + SealedSecret.

## Klasör Yapısı

| Klasör | İçerik |
|--------|--------|
| `frontend/` | Expo + React mobil/web uygulaması (deploy paneli) |
| `backend/` | .NET 8 Web API (dispatch, overlay üretimi, ArgoCD erişimi) |
| `.github/workflows/` | GitHub Actions: build, validate-overlays, runner-test |
| `.github/actions/` | Composite action'lar (build-datactive, validate-overlay) |
| `manifests/base/` | Kustomize base (deployment, service, config, sealed-secret) |
| `manifests/overlays/` | Build'e özel overlay'ler (namespace, tag, schema) |
| `manifests/arc/` | ARC runner + buildkitd kurulum manifestleri |
| `manifests/argocd/` | ArgoCD AppProject (K2.19) |
| `scripts/` | Operasyon scriptleri (seal-secret, argocd-app, audit-ns...) |
| `docs/` | Sözleşme ve dokümantasyon |
| `azure-pipelines.yml` | Azure DevOps geçiş pipeline'ı (K2.17) |

## Kurulum

### Backend (.NET 8)
```bash
cd backend
dotnet restore
dotnet run
```
API: http://localhost:5000

### Frontend (Expo + React)
```bash
cd frontend
npm install
npm start
```

### Altyapı (cluster)
- Runner + buildkitd: `manifests/arc/` → [ARC_SETUP.md](docs/ARC_SETUP.md)
- Sealed Secrets: [SEALED_SECRETS_SETUP.md](docs/SEALED_SECRETS_SETUP.md)
- ArgoCD: [AppProject](manifests/argocd/appproject.yaml)
- Harbor: [HARBOR_SETUP.md](docs/HARBOR_SETUP.md)

## Dokümanlar

| Doküman | İçerik |
|---------|--------|
| [CONTRACT.md](docs/CONTRACT.md) | Entegrasyon sözleşmesi (tüm kararlar) |
| [RUNBOOK.md](docs/RUNBOOK.md) | Canlıda build/hatalar (operasyon) |
| [ROTATION.md](docs/ROTATION.md) | Secret rotation |
| [AZURE_MIGRATION.md](docs/AZURE_MIGRATION.md) | GH → Azure geçişi |
| [DEV_ENV.md](docs/DEV_ENV.md) | Geliştirme ortamı |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Branch stratejisi |

## Geliştirme

Branch stratejisi ve katkı kuralları: [CONTRIBUTING.md](CONTRIBUTING.md)
