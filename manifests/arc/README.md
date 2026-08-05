# ARC — Runner Altyapısı

Actions Runner Controller tabanlı self-hosted runner yapılandırması.

## İçerik

| Dosya | Açıklama |
|-------|----------|
| `controller-values.yaml` | ARC controller Helm değerleri (arc-systems) |
| `runner-values.yaml` | Runner scale set Helm değerleri — ephemeral + autoscaling (arc-runners) |
| `auth-secret.yaml` | GitHub auth secret şablonu (GitHub App, gerçek değerler SEALED) |

## Kurulum

Boss tarafından uygulanır → `docs/ARC_SETUP.md`

## Önemli Kararlar

- **Ephemeral runner'lar** (`podRetention: none`) — job başına taze pod, izolasyon
- **Autoscaling** (0-10) — boşta maliyet yok, yoğunlukta otomatik büyüme
- **GitHub App auth** — runner'a özel kimlik (PAT değil)
- **Resources:** 2-4 CPU / 4-8Gi — Kaniko build için
- **containerMode: kubernetes** — DinD yok, Kaniko build (K2.8) için hazır

## Not

`auth-secret.yaml` şablonudur. Gerçek GitHub App değerleri SealedSecret'a dönüştürülerek repo'ya konur (K2.12 entegrasyonu).
