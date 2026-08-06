# RUNBOOK — Operasyon Rehberi

Canlıda bir build'in akışı ve karşılaşılan hataların çözümü.

## Build Akışı (Canlı)

```
1. Deploy Paneli → POST /api/builds (branch_web, branch_server, schema)
2. Backend → tag üretir (K1.11) → GitHub Actions dispatch (K1.8)
3. Runner (ARC) job'ı alır → job container'da git clone ×2 (K2.8)
4. buildctl → buildkitd (cluster'da daemon) → image build (K2.8)
5. Image → registry'ye push (K2.10)
6. Backend → overlay üretir + commit + push (K1.10)
7. ArgoCD overlay'i algılar → Application sync (K2.13)
8. Uygulama build-{tarih}-{ticket} namespace'inde deploy olur
```

## Durum Kontrolü

```bash
# Build durumu (panel / API)
gh run list --workflow build.yml
curl localhost:5000/api/builds

# ArgoCD sync/health
curl -H "Authorization: Bearer $ARGOCD_TOKEN" \
  "http://localhost:18080/api/v1/applications/<app>"

# Pod'lar
kubectl get pods -n build-<tarih>-<ticket>
```

## Sık Karşılaşılan Hatalar

### 1. Workflow "queued" takılı
- **Neden:** Runner yok / ARC offline
- **Kontrol:** `kubectl get pods -n arc-runners`
- **Çözüm:** ARC listener çalışıyor mu; runner online mı (`gh api .../actions/runners`)

### 2. ImagePullBackOff
- **Neden:** Image registry'de yok veya regcred eksik
- **Kontrol:** `kubectl describe pod -n <ns> | grep -A5 Events`
- **Çözüm:** Image tag registry'de mi; `regcred` secret'ı namespace'te mi (K2.10)

### 3. ArgoCD "OutOfSync" takılı
- **Neden:** Repo'da değişiklik var ama sync olmadı
- **Çözüm:** `argocd app sync <app>` veya Re-Sync butonu (panelde)

### 4. Build fail — "DatActive Build" step'i
- **Neden:** Checkout, buildkit veya push hatası
- **Kontrol:** `gh run view <id> --log-failed`
- **Sık sebep:** PAT_TOKEN geçersiz (private repo checkout) → `secrets.PAT_TOKEN`'ı yenile (C2.1)

### 5. Kaniko/Buildkit hatası
- **Neden:** Buildkitd down veya registry erişimi
- **Kontrol:** `kubectl get pods -n arc-runners -l app=buildkitd`
- **Çözüm:** Buildkitd restart; registry adresi/insecure config (K2.10)

### 6. SealedSecret çözülemiyor
- **Neden:** Controller yok veya private key değişti
- **Kontrol:** `kubectl get pods -n kube-system | grep sealed`
- **Çözüm:** Controller kur (K2.3); key yedeğinden geri yükle (ROTATION.md)

## Smoke Test (Uçtan Uca)

```bash
# 1. Build tetikle (elle)
gh workflow run build.yml \
  -f branch_web=main -f branch_server=main \
  -f schema=schema_test -f tag=smoke-$(date +%Y%m%d%H%M%S)

# 2. Build success mi?
gh run watch

# 3. Image registry'de mi?
curl http://localhost:5000/v2/datateam/datactive.web/tags/list

# 4. Overlay push edildi mi? ArgoCD sync oldu mu?
#    (panelde "ArgoCD Sync: Synced, Health: Healthy")

# 5. Pod çalışıyor mu?
kubectl get pods -A | grep build-
```

## Rollback

```bash
# Build'i geri al (overlay revert)
git revert <overlay-commit> && git push   # ArgoCD eski haline sync eder

# Uygulamayı tamamen kaldır
kubectl delete namespace build-<tarih>-<ticket>
```
