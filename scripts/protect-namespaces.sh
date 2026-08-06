#!/usr/bin/env bash
set -euo pipefail

# Build namespace koruma scripti (K2.16)
# Tüm build namespace'lerine koruma etiketi ekler — C6.1 kararı gereği
# namespace'ler KALICI tutulur; bu etiket yanlışlıkla silinmeyi zorlaştırır.
#
# Kullanım:
#   bash scripts/protect-namespaces.sh          # tüm build-* namespace'lerini korur
#   bash scripts/protect-namespaces.sh <ns>     # tek namespace korur

LABEL="datactive.gitops/protected=true"
NS_FILTER="build-"

protect() {
  local ns="$1"
  if kubectl get namespace "$ns" >/dev/null 2>&1; then
    kubectl label namespace "$ns" "$LABEL" --overwrite >/dev/null
    echo "KORUNDU: $ns"
  else
    echo "YOK: $ns (namespace mevcut değil)"
  fi
}

if [[ $# -eq 1 ]]; then
  protect "$1"
  exit 0
fi

echo "==> Tüm build namespace'leri korunuyor (C6.1: kalıcı tut)"
COUNT=0
for ns in $(kubectl get namespaces -o jsonpath='{.items[*].metadata.name}' | tr ' ' '\n' | grep "^${NS_FILTER}" || true); do
  protect "$ns"
  COUNT=$((COUNT+1))
done

echo ""
echo "OK: $COUNT namespace korundu."
echo "    Koruma etiketi: $LABEL"
