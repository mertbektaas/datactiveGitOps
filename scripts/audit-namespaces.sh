#!/usr/bin/env bash
set -euo pipefail

# Build namespace ↔ ArgoCD Application denetim scripti (K2.16)
# Her build namespace'i için ArgoCD'de karşılık gelen Application var mı kontrol eder.
# C6.1: namespace'ler kalıcı — bu script "hepsi yerinde ve izleniyor mu?" doğrular.
#
# Kullanım:
#   ARGOCD_SERVER=localhost:18080 ARGOCD_TOKEN=<token> bash scripts/audit-namespaces.sh
#
# Ortam değişkenleri:
#   ARGOCD_SERVER  (default: localhost:18080)
#   ARGOCD_TOKEN   (ArgoCD apiKey — build-* get yetkili)

SERVER="${ARGOCD_SERVER:-localhost:18080}"
TOKEN="${ARGOCD_TOKEN:?ARGOCD_TOKEN gerekli}"
NS_FILTER="build-"
API_BASE="https://${SERVER#https://}"

echo "==> Namespace'ler taranıyor (filtre: ${NS_FILTER}*)"
NAMESPACES=$(kubectl get namespaces -o jsonpath='{.items[*].metadata.name}' | tr ' ' '\n' | grep "^${NS_FILTER}" || true)

if [[ -z "$NAMESPACES" ]]; then
  echo "Build namespace'i bulunamadı."
  exit 0
fi

TOTAL=0
PROTECTED=0
NO_APP=0

for ns in $NAMESPACES; do
  TOTAL=$((TOTAL+1))

  # Koruma etiketi kontrolü
  if kubectl get namespace "$ns" -o jsonpath='{.metadata.labels.datactive\.gitops/protected}' 2>/dev/null | grep -q "true"; then
    PROTECTED=$((PROTECTED+1))
  else
    echo "UYARI: $ns — koruma etiketi YOK (scripts/protect-namespaces.sh çalıştırın)"
  fi

  # ArgoCD Application eşleşmesi
  if curl -sk -H "Authorization: Bearer $TOKEN" \
    "$API_BASE/api/v1/applications/$ns" 2>/dev/null | grep -q "\"name\":\"$ns\""; then
    :
  else
    echo "UYARI: $ns — ArgoCD Application bulunamadı (K2.13 scripti çalıştırın)"
    NO_APP=$((NO_APP+1))
  fi
done

echo ""
echo "=== ÖZET ==="
echo "Toplam namespace: $TOTAL"
echo "Korumalı:         $PROTECTED"
echo "App'siz:          $NO_APP"
echo ""
if [[ $NO_APP -eq 0 ]]; then
  echo "OK: Tüm namespace'ler korumalı ve ArgoCD'de izleniyor."
else
  echo "DİKKAT: $NO_APP namespace ArgoCD'de izlenmiyor."
fi
