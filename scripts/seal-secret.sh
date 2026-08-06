#!/usr/bin/env bash
set -euo pipefail

# Sealed Secret üretim scripti.
# Düz Secret'ı kubeseal ile şifreler, base'e yazar ve kustomization'a ekler.
#
# Kullanım:
#   bash scripts/seal-secret.sh <secret-yaml-yolu> <cert-dosyası>
# Örnek:
#   bash scripts/seal-secret.sh manifests/base/datactive-secret.yaml docs/cert/sealed-secrets-cert.pem
#
# Ön koşul: kubeseal CLI + controller'ın public key'i (docs/cert/sealed-secrets-cert.pem)

SECRET_FILE="${1:?Secret YAML yolu gerekli (örn: manifests/base/datactive-secret.yaml)}"
CERT_FILE="${2:?Public key dosyası gerekli (örn: docs/cert/sealed-secrets-cert.pem)}"

OUTPUT_FILE="manifests/base/datactive-sealed-secret.yaml"
KUSTOMIZATION="manifests/base/kustomization.yaml"

[[ -f "$SECRET_FILE" ]] || { echo "HATA: $SECRET_FILE bulunamadı"; exit 1; }
[[ -f "$CERT_FILE" ]] || { echo "HATA: $CERT_FILE bulunamadı — önce: kubeseal --fetch-cert > $CERT_FILE"; exit 1; }

echo "==> Şifreleniyor: $SECRET_FILE"
echo "    Cert: $CERT_FILE"

# Düz Secret'ı şifrele → SealedSecret üret
kubeseal --format yaml --cert "$CERT_FILE" < "$SECRET_FILE" > "$OUTPUT_FILE"

echo "==> Oluştu: $OUTPUT_FILE"

# Şifreli dosyada düz metin olmadığını doğrula
if grep -qiE "stringData|data:" "$OUTPUT_FILE" && grep -qE "password|secret|token" "$OUTPUT_FILE"; then
  echo "UYARI: Çıktıda veri alanı görünüyor — şifreleme başarısız olabilir, kontrol edin."
fi

# kustomization.yaml'a resources olarak ekle (yoksa)
if ! grep -q "datactive-sealed-secret.yaml" "$KUSTOMIZATION"; then
  echo "==> kustomization.yaml'a ekleniyor: datactive-sealed-secret.yaml"
  # resources listesine ekle (son resources satırından sonra)
  sed -i '' '/^resources:/a\
  - datactive-sealed-secret.yaml
' "$KUSTOMIZATION"
else
  echo "==> kustomization.yaml'da zaten var (değişiklik yok)"
fi

echo ""
echo "OK: SealedSecret üretildi ve base'e eklendi."
echo "    Cluster'da çözüm için: kubectl apply (Sealed Secrets controller gerekli, K2.3)"
echo "    Eski düz secret artık gerekmiyorsa silinebilir: git rm $SECRET_FILE"
