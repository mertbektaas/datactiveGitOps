#!/usr/bin/env bash
set -euo pipefail

# ArgoCD Application create + sync scripti (K2.13)
# Her build için: Application oluştur (upsert) + sync tetikle.
#
# Kullanım:
#   bash scripts/argocd-app.sh <namespace> <overlay-path> [image-override]
# Örnek:
#   bash scripts/argocd-app.sh build-20260806-K1-5 manifests/overlays/build-20260806-K1-5
#
# Ortam değişkenleri:
#   ARGOCD_SERVER  (default: localhost:18080)
#   ARGOCD_TOKEN   (ci-builder token — build-* yetkili, K2.14)
#   ARGOCD_REPO    (git repo URL — ArgoCD'ye kayıtlı)

SERVER="${ARGOCD_SERVER:-localhost:18080}"
TOKEN="${ARGOCD_TOKEN:?ARGOCD_TOKEN gerekli (K2.14 token)}"
REPO="${ARGOCD_REPO:-https://github.com/mertbektaas/datactiveGitOps.git}"
DEST_SERVER="https://kubernetes.default.svc"

NAMESPACE="${1:?Namespace gerekli (örn: build-20260806-K1-5)}"
OVERLAY_PATH="${2:?Overlay path gerekli (örn: manifests/overlays/build-xxx)}"

ARGO=(--server "$SERVER" --auth-token "$TOKEN" --insecure --grpc-web)

echo "==> [1/4] Namespace hazırlanıyor: $NAMESPACE"
kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f - >/dev/null

echo "==> [2/4] Application oluşturuluyor (upsert): $NAMESPACE"
argocd app create "$NAMESPACE" "${ARGO[@]}" \
  --repo "$REPO" \
  --path "$OVERLAY_PATH" \
  --dest-server "$DEST_SERVER" \
  --dest-namespace "$NAMESPACE" \
  --upsert
echo "    (oluşturuldu / güncellendi)"

echo "==> [3/4] Repo refresh + sync"
argocd app get "$NAMESPACE" "${ARGO[@]}" --hard-refresh >/dev/null 2>&1 || true
argocd app sync "$NAMESPACE" "${ARGO[@]}" 2>&1 | tail -5

echo "==> [4/4] Sync durumu"
argocd app get "$NAMESPACE" "${ARGO[@]}" -o jsonpath='{.status.sync.status}' 2>/dev/null
echo ""
echo "OK: Application '$NAMESPACE' hazır ve sync edildi."
