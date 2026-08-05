#!/usr/bin/env bash
set -euo pipefail

# ARC runner doğrulama scripti.
# Kurulum sonrası tek komut: bash manifests/arc/verify-arc.sh
# Şunları doğrular: runner online → workflow tetikle → job runner'da koştu → sonuç raporla

REPO="mertbektaas/datactiveGitOps"
WORKFLOW="runner-test.yml"
RUNNER_GROUP="arc-runner-set"

echo "==> [1/5] Runner durumu kontrol ediliyor..."
RUNNER_STATUS=$(gh api "repos/$REPO/actions/runners" --jq '.runners[] | select(.labels[].name == "'$RUNNER_GROUP'") | .status' 2>/dev/null || echo "UNKNOWN")
if [[ "$RUNNER_STATUS" != "online" ]]; then
  echo "HATA: Runner '$RUNNER_GROUP' online değil. Durum: '$RUNNER_STATUS'"
  echo "      K2.6 kurulumunu kontrol et (helm list -A, kubectl get pods -n arc-runners)"
  exit 1
fi
echo "Runner '$RUNNER_GROUP' durumu: ONLINE"

echo "==> [2/5] Test workflow tetikleniyor..."
gh workflow run "$WORKFLOW" \
  -f test_payload="arc-verify-$(date +%s)" \
  --repo "$REPO"

echo "==> [3/5] Run sonucu bekleniyor..."
gh run watch --repo "$REPO" --exit-status --interval 5 > /dev/null 2>&1 || true

echo "==> [4/5] Run detayları alınıyor..."
RUN_ID=$(gh run list --repo "$REPO" --workflow "$WORKFLOW" --limit 1 --json databaseId --jq '.[0].databaseId')
RUN_JSON=$(gh run view "$RUN_ID" --repo "$REPO" --json status,conclusion,headBranch --jq '{status,conclusion,headBranch}')

echo "Run #$RUN_ID: $(echo "$RUN_JSON" | jq -r '.conclusion') (branch: $(echo "$RUN_JSON" | jq -r '.headBranch'))"

if [[ "$(echo "$RUN_JSON" | jq -r '.conclusion')" != "success" ]]; then
  echo "HATA: Run başarısız. Detay: gh run view $RUN_ID --repo $REPO"
  exit 1
fi

echo "==> [5/5] Runner pod ismi doğrulanıyor..."
if command -v kubectl >/dev/null 2>&1; then
  POD_NAME=$(kubectl get pods -n arc-runners -l "actions.github.com/runner-label=$RUNNER_GROUP" \
    --sort-by=.metadata.creationTimestamp -o jsonpath='{.items[-1].metadata.name}' 2>/dev/null || echo "bulunamadı")
  echo "Runner pod: $POD_NAME"
  kubectl logs -n arc-runners "$POD_NAME" --tail=5 2>/dev/null | grep -i "echo\|test_payload" | tail -3 || true
else
  echo "kubectl yok — pod ismi: gh run view $RUN_ID log'larından görülebilir"
fi

echo ""
echo "OK: Runner testi başarılı."
