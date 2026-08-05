#!/usr/bin/env bash
set -euo pipefail

# Sealed Secrets zinciri doğrulama scripti.
# Kurulum sonrası çalıştırılır; dummy secret üretir, şifreler, uygular ve çözüldüğünü doğrular.

CONTROLLER_NAME="sealed-secrets-controller"
CONTROLLER_NS="kube-system"
CERT_FILE="docs/cert/sealed-secrets-cert.pem"
OUTPUT_FILE="manifests/sealed-secrets/test-sealedsecret.yaml"
SECRET_NAME="testvalue"
SECRET_VALUE="testvalue-dummy-secret"

# 1. Public key temin et (yoksa controller'dan çek)
if [[ ! -f "$CERT_FILE" ]]; then
  kubeseal --fetch-cert --controller-name "$CONTROLLER_NAME" --controller-namespace "$CONTROLLER_NS" > "$CERT_FILE"
fi

# 2. Dummy secret üret ve şifrele
kubectl create secret generic "$SECRET_NAME" \
  --from-literal=value="$SECRET_VALUE" \
  --dry-run=client -o yaml | \
  kubeseal --format yaml --cert "$CERT_FILE" > "$OUTPUT_FILE"

echo "SealedSecret oluşturuldu: $OUTPUT_FILE"

# 3. Şifreli dosyada düz metin olmadığını doğrula
if grep -q "$SECRET_VALUE" "$OUTPUT_FILE"; then
  echo "HATA: SealedSecret içinde düz metin bulundu."
  exit 1
fi

# 4. Cluster'a uygula
kubectl apply -f "$OUTPUT_FILE"

# 5. Controller'ın çözdüğünü doğrula
kubectl wait --for=jsonpath='{.data.value}' secret/"$SECRET_NAME" --timeout=30s

RESOLVED=$(kubectl get secret "$SECRET_NAME" -o jsonpath='{.data.value}' | base64 -d)
if [[ "$RESOLVED" != "$SECRET_VALUE" ]]; then
  echo "HATA: Çözülen değer beklenenle eşleşmiyor."
  exit 1
fi

echo "OK: Secret çözüldü -> $RESOLVED"
