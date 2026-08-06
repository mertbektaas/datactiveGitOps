# GitHub Actions → Azure DevOps Geçiş Rehberi (K2.17)

## Özet

Build mantığı GitHub'dan bağımsız olduğu için (composite action / script), geçiş **pipeline dosyasının değiştirilmesi** ile sınırlıdır.

## 1. Karşılaştırma (GH vs Azure)

| GitHub Actions | Azure DevOps |
|----------------|--------------|
| `.github/workflows/build.yml` | `azure-pipelines.yml` |
| `workflow_dispatch` inputs | `parameters` |
| `on: push/pull_request` | `trigger: none` (manual) |
| `runs-on: arc-runner-set` | `pool: vmImage` |
| `steps: run` | `steps: script` |
| `secrets.PAT_TOKEN` | `$(PAT_TOKEN)` (pipeline secret) |
| `${{ inputs.x }}` | `$(x)` |

## 2. Geçiş Adımları

1. **Azure DevOps'ta pipeline oluştur** → repo bağla (datactiveGitOps)
2. **`azure-pipelines.yml`** dosyasını kullan (repo'da hazır)
3. **Secret ekle:** Pipeline → Variables → `PAT_TOKEN` (GH'daki ile aynı)
4. **Agent seçimi:**
   - Test: `ubuntu-latest` (Microsoft-hosted)
   - Production (cluster içi): self-hosted agent → `pool: <agent-pool>` (ARC yerine)
5. **Buildkit erişimi:**
   - Cluster içi agent: `buildkitHost` → `tcp://buildkitd...` (aynı)
   - Dış agent: buildkitd'ye ağ erişimi gerekir veya registry'ye push
6. **Registry adresi** (`variables.registry`): production'da `harbor.datactive.net`

## 3. Değişmeyenler (Aynı Kalır)

- Build komutları (buildctl, git clone) — K2.8 mantığı
- Overlay üretimi + commit (K1.10 — GitOps)
- ArgoCD Application + sync (K2.13) — Azure'dan tetiklenebilir
- Sealed Secrets, key yönetimi (K2.12/18)

## 4. Ne Zaman Geçilir

- GitHub Actions kullanımdan kaldırılınca
- PAT token'ları iptal edilir (C2.1: geçici ömür)
- ARC runner'lar kaldırılır (Azure agent ile değişir)
- Buildkitd cluster'da kalır (değişmez)
