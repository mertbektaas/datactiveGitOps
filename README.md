# datactiveGitOps

Datactive ürününün (datateam-web + datateam-core.server) otomatik build & deploy sistemi.

Kullanıcı branch ve DB schema'sı seçer → kod GitHub Actions ile build edilir → image Harbor'a gönderilir → ArgoCD manifestleri güncellenir → Kubernetes cluster'ına otomatik dağıtılır. Her build için yeni namespace + Kustomize overlay + Sealed Secret üretilir.

## Klasör Yapısı

- `frontend/` — Expo + React mobil/web uygulaması
- `backend/` — .NET 8 Web API sunucusu
- `workflows/` — GitHub Actions CI/CD dosyaları
- `manifests/` — Kubernetes Kustomize manifestleri (base + overlays)
- `docs/` — Sözleşmeler ve dokümantasyon
