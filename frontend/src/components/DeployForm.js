import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, TouchableOpacity, ActivityIndicator } from 'react-native';
import { getSchemas, dispatchBuild } from '../services/api';

export default function DeployForm({ selectedPair, onBuildSuccess }) {
  const [schemas, setSchemas] = useState([]);
  const [selectedSchema, setSelectedSchema] = useState('');
  const [loadingSchemas, setLoadingSchemas] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [buildResult, setBuildResult] = useState(null);
  const [errorMessage, setErrorMessage] = useState(null);

  useEffect(() => {
    const fetchSchemasList = async () => {
      setLoadingSchemas(true);
      try {
        const data = await getSchemas();
        setSchemas(data || []);
        if (data && data.length > 0) {
          setSelectedSchema(data[0].id);
        }
      } catch (err) {
        console.error('Schema fetch error:', err);
      } finally {
        setLoadingSchemas(false);
      }
    };

    fetchSchemasList();
  }, []);

  const handleStartDeploy = async () => {
    setErrorMessage(null);
    setBuildResult(null);

    if (!selectedPair?.branchWeb || !selectedPair?.branchServer) {
      setErrorMessage('Lütfen önce Web ve Server branch seçimlerini yapın.');
      return;
    }

    if (!selectedSchema) {
      setErrorMessage('Lütfen veritabanı şeması (schema) seçin.');
      return;
    }

    setSubmitting(true);

    try {
      const payload = {
        branch_web: selectedPair.branchWeb,
        branch_server: selectedPair.branchServer,
        schema: selectedSchema,
        tag: 'auto'
      };

      const result = await dispatchBuild(payload);
      setBuildResult(result);

      if (onBuildSuccess) {
        onBuildSuccess(result);
      }
    } catch (err) {
      const detail = err.response?.data?.error || err.response?.data?.detail || err.message;
      setErrorMessage(`Deploy tetiklenirken hata oluştu: ${detail}`);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <View style={styles.container}>
      <Text style={styles.sectionTitle}>2. Veritabanı Şeması ve Deploy Tetikleme</Text>

      {/* Schema Selector */}
      <View style={styles.fieldGroup}>
        <Text style={styles.label}>🗄️ Veritabanı Şeması (DB Schema):</Text>
        {loadingSchemas ? (
          <ActivityIndicator size="small" color="#3b82f6" />
        ) : (
          <View style={styles.chipContainer}>
            {schemas.map((s) => (
              <TouchableOpacity
                key={s.id}
                style={[styles.chip, selectedSchema === s.id && styles.chipActive]}
                onPress={() => setSelectedSchema(s.id)}
              >
                <Text style={[styles.chipText, selectedSchema === s.id && styles.chipTextActive]}>
                  {s.id}
                </Text>
              </TouchableOpacity>
            ))}
          </View>
        )}
      </View>

      {/* Error Banner */}
      {errorMessage && (
        <View style={styles.errorBox}>
          <Text style={styles.errorText}>⚠️ {errorMessage}</Text>
        </View>
      )}

      {/* Deploy Submit Button */}
      <TouchableOpacity
        style={[styles.deployButton, submitting && styles.deployButtonDisabled]}
        onPress={handleStartDeploy}
        disabled={submitting}
      >
        {submitting ? (
          <View style={styles.buttonContent}>
            <ActivityIndicator size="small" color="#ffffff" style={styles.spinner} />
            <Text style={styles.deployButtonText}>GitHub Actions Tetikleniyor...</Text>
          </View>
        ) : (
          <Text style={styles.deployButtonText}>🚀 Deploy'u Başlat (Canlı Tetikle)</Text>
        )}
      </TouchableOpacity>

      {/* Success Result Card */}
      {buildResult && (
        <View style={styles.resultCard}>
          <View style={styles.resultHeader}>
            <Text style={styles.resultIcon}>✅</Text>
            <Text style={styles.resultTitle}>Build Başarıyla Tetiklendi ve Sıraya Alındı!</Text>
          </View>
          <View style={styles.resultDetails}>
            <Text style={styles.detailRow}><Text style={styles.bold}>Build ID:</Text> {buildResult.buildId}</Text>
            <Text style={styles.detailRow}><Text style={styles.bold}>Namespace:</Text> {buildResult.namespace}</Text>
            <Text style={styles.detailRow}><Text style={styles.bold}>Tag:</Text> {buildResult.tag}</Text>
            <Text style={styles.detailRow}>
              <Text style={styles.bold}>Durum:</Text> <Text style={styles.statusQueued}>● {buildResult.status}</Text>
            </Text>
          </View>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    width: '100%',
    backgroundColor: '#1e293b',
    borderRadius: 16,
    padding: 20,
    borderWidth: 1,
    borderColor: '#334155',
    marginBottom: 20,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#f8fafc',
    marginBottom: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#334155',
    paddingBottom: 8,
  },
  fieldGroup: {
    marginBottom: 18,
  },
  label: {
    fontSize: 13,
    fontWeight: '600',
    color: '#94a3b8',
    marginBottom: 8,
  },
  chipContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  chip: {
    backgroundColor: '#0f172a',
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#334155',
  },
  chipActive: {
    backgroundColor: '#0284c7',
    borderColor: '#38bdf8',
  },
  chipText: {
    color: '#cbd5e1',
    fontSize: 13,
    fontWeight: '500',
  },
  chipTextActive: {
    color: '#ffffff',
    fontWeight: 'bold',
  },
  deployButton: {
    backgroundColor: '#16a34a',
    paddingVertical: 14,
    borderRadius: 10,
    alignItems: 'center',
    justifyContent: 'center',
    shadowColor: '#16a34a',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 6,
    marginTop: 6,
  },
  deployButtonDisabled: {
    backgroundColor: '#374151',
    shadowOpacity: 0,
  },
  buttonContent: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  spinner: {
    marginRight: 8,
  },
  deployButtonText: {
    color: '#ffffff',
    fontWeight: 'bold',
    fontSize: 15,
  },
  errorBox: {
    backgroundColor: '#7f1d1d',
    borderRadius: 8,
    padding: 12,
    marginBottom: 14,
  },
  errorText: {
    color: '#fca5a5',
    fontSize: 13,
  },
  resultCard: {
    backgroundColor: '#064e3b',
    borderRadius: 12,
    padding: 16,
    marginTop: 18,
    borderWidth: 1,
    borderColor: '#059669',
  },
  resultHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 10,
  },
  resultIcon: {
    fontSize: 18,
    marginRight: 8,
  },
  resultTitle: {
    color: '#34d399',
    fontWeight: 'bold',
    fontSize: 14,
  },
  resultDetails: {
    backgroundColor: '#022c22',
    borderRadius: 8,
    padding: 10,
    gap: 4,
  },
  detailRow: {
    color: '#e2e8f0',
    fontSize: 13,
  },
  bold: {
    fontWeight: 'bold',
    color: '#a7f3d0',
  },
  statusQueued: {
    color: '#fbbf24',
    fontWeight: 'bold',
  },
});
