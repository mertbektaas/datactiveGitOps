# ARC — Actions Runner Controller Kurulum

Self-hosted GitHub Actions runner'larının k3s cluster'ında yönetimi. Ephemeral runner'lar + otomatik ölçekleme + GitHub App auth.

## Mimari

```
arc-systems namespace       arc-runners namespace
┌────────────────────┐      ┌──────────────────────────┐
│ ARC Controller      │      │ Runner Scale Set (ephemeral)│
│ (helm: arc)         │      │ (helm: arc-runner-set)      │
│ • CRD'leri yönetir  │ ───► │ • İstek geldikçe pod açar  │
│ • GitHub API izler  │      │ • Job bitince pod siler    │
└────────────────────┘      └──────────────────────────┘
```

## Ön Koşullar

- k3s cluster, admin yetkisi
- Helm 3
- GitHub hesabı (auth için)

## 1. GitHub App Oluştur (Auth)

PAT yerine GitHub App tercih edilir — runner'a özel kimlik, kişisel hesaptan bağımsız, Azure geçişinde kolay iptal.

1. GitHub → Settings → Developer settings → GitHub Apps → New GitHub App
2. Ayarlar:
   - Repository permissions: `actions: read`, `metadata: read`
   - Organization permissions: `self-hosted runners: write` (org runner ise)
3. App'i yalnızca `datactiveGitOps` repo'suna install et
4. Private key indir (`github_app_private_key`)

## 2. Auth Secret Oluştur

```bash
kubectl create secret generic arc-auth \
  --namespace arc-runners \
  --from-literal=github_app_id=<APP_ID> \
  --from-literal=github_app_installation_id=<INSTALLATION_ID> \
  --from-literal=github_app_private_key="$(cat <indirilen-key.pem>)"
```

## 3. Controller Kur

```bash
NAMESPACE="arc-systems"

helm install arc \
  --namespace "${NAMESPACE}" \
  --create-namespace \
  --values manifests/arc/controller-values.yaml \
  oci://ghcr.io/actions/actions-runner-controller-charts/gha-runner-scale-set-controller
```

## 4. Runner Scale Set Kur

```bash
NAMESPACE="arc-runners"

helm install arc-runner-set \
  --namespace "${NAMESPACE}" \
  --create-namespace \
  --values manifests/arc/runner-values.yaml \
  oci://ghcr.io/actions/actions-runner-controller-charts/gha-runner-scale-set
```

## 5. Doğrula

```bash
# Controller ve listener pod'ları
kubectl get pods -n arc-systems
kubectl get pods -n arc-runners

# Runner'lar GitHub'da görünüyor mu
gh api repos/mertbektaas/datactiveGitOps/actions/runners --jq '.runners[] | {name, status}'
```

## 6. Workflow'da Kullanım

`runs-on: arc-runner-set` (installation name). Örnek — K2.5'in build.yml'ını günceller:

```yaml
jobs:
  build:
    runs-on: arc-runner-set   # ← self-hosted
```

## 7. Autoscaling Davranışı

- Boşta: **0 runner** pod (maliyet yok)
- Job geldi: ARC 60 sn içinde pod açar (maxRunnerScaleUpTime)
- Yoğunluk: max 10 runner (capacity.maxRunners)
- Job bitti: pod silinir (podRetention: none → ephemeral)

## Kriz / Edge Case Notları

- **Runner pod'u ölürse:** ARC yeni pod açar, job yeniden scheduler olur
- **GitHub App key sızdırırsa:** App'i iptal et, yeni key → Secret güncelle (pod'lar restart)
- **Kuyruk uzarsa:** maxRunners artır → capacity.maxRunners
- **Controller ölürse:** Runner'lar ayakta kalır (mevcut job'lar biter), yeni job alımı durur; controller restart edilince kaldığı yerden devam eder
- **Cluster yeniden kurulursa:** ARC + scale set helm ile yeniden kurulur; Secret (App key) yedekte durmalı

## Rollback

```bash
helm uninstall arc-runner-set --namespace arc-runners
helm uninstall arc --namespace arc-systems
kubectl delete namespace arc-systems arc-runners
```
