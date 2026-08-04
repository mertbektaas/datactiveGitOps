# datactiveGitOps
Datactive ürününün (datateam-web + datateam-core.server) otomatik build & deploy sistemi. Kullanıcı branch ve DB schema'sı seçer → kod GitHub Actions ile build edilir → image Harbor'a gönderilir → ArgoCD manifestleri güncellenir → Kubernetes cluster'ına otomatik dağıtılır. Her build için yeni namespace + Kustomize overlay + Sealed Secret üretilir.
