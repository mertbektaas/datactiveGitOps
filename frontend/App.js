import React, { useState, useEffect } from 'react';
import { 
  StyleSheet, 
  Text, 
  View, 
  TouchableOpacity, 
  ActivityIndicator, 
  SafeAreaView, 
  ScrollView, 
  useWindowDimensions 
} from 'react-native';
import { StatusBar } from 'expo-status-bar';
import axios from 'axios';

const API_BASE_URL = process.env.EXPO_PUBLIC_API_URL || 'http://localhost:8080';

export default function App() {
  const { width } = useWindowDimensions();
  const isMobile = width < 768;

  const [healthStatus, setHealthStatus] = useState(null);
  const [loading, setLoading] = useState(false);
  const [lastCheck, setLastCheck] = useState(null);

  const fetchHealthCheck = async () => {
    setLoading(true);
    try {
      const response = await axios.get(`${API_BASE_URL}/health`);
      setHealthStatus({
        ok: true,
        status: response.data.status || 'Healthy',
        service: response.data.service || 'datactive-backend',
        timestamp: response.data.timestamp || new Date().toISOString()
      });
    } catch (error) {
      setHealthStatus({
        ok: false,
        status: 'Unreachable',
        error: error.message
      });
    } finally {
      setLoading(false);
      setLastCheck(new Date().toLocaleTimeString());
    }
  };

  useEffect(() => {
    fetchHealthCheck();
  }, []);

  return (
    <SafeAreaView style={styles.container}>
      <StatusBar style="light" />
      <ScrollView contentContainerStyle={styles.scrollContent}>
        {/* Header */}
        <View style={styles.header}>
          <Text style={styles.logoText}>⚡ Datactive<Text style={styles.logoAccent}>GitOps</Text></Text>
          <Text style={styles.subtitleText}>Ölçeklenebilir Kubernetes Dağıtım & Boru Hattı Portalı</Text>
        </View>

        {/* Main Card Container */}
        <View style={[styles.card, isMobile && styles.cardMobile]}>
          <View style={styles.cardHeader}>
            <Text style={styles.cardTitle}>Backend API Bağlantı Durumu</Text>
            <TouchableOpacity 
              style={styles.refreshButton} 
              onPress={fetchHealthCheck} 
              disabled={loading}
            >
              {loading ? (
                <ActivityIndicator size="small" color="#ffffff" />
              ) : (
                <Text style={styles.refreshButtonText}>🔄 Yenile</Text>
              )}
            </TouchableOpacity>
          </View>

          <View style={styles.statusRow}>
            <Text style={styles.label}>Hedef API Adresi:</Text>
            <Text style={styles.urlValue}>{API_BASE_URL}</Text>
          </View>

          <View style={styles.statusRow}>
            <Text style={styles.label}>Servis Durumu:</Text>
            {healthStatus ? (
              <View style={[styles.badge, healthStatus.ok ? styles.badgeSuccess : styles.badgeError]}>
                <Text style={styles.badgeText}>
                  {healthStatus.ok ? `● ${healthStatus.status}` : `✖ ${healthStatus.status}`}
                </Text>
              </View>
            ) : (
              <Text style={styles.loadingText}>Kontrol ediliyor...</Text>
            )}
          </View>

          {healthStatus && healthStatus.ok && (
            <View style={styles.infoBox}>
              <Text style={styles.infoText}>
                <Text style={styles.boldText}>Servis Adı:</Text> {healthStatus.service}
              </Text>
              <Text style={styles.infoText}>
                <Text style={styles.boldText}>Son Kontrol:</Text> {lastCheck}
              </Text>
            </View>
          )}

          {healthStatus && !healthStatus.ok && (
            <View style={[styles.infoBox, styles.infoBoxError]}>
              <Text style={styles.errorText}>
                ⚠️ Backend API sunucusuna erişilemedi ({healthStatus.error}). Lütfen backend servisinin (port 8080) çalıştığından emin olun.
              </Text>
            </View>
          )}
        </View>

        {/* Responsive Grid Info */}
        <View style={[styles.grid, isMobile && styles.gridMobile]}>
          <View style={styles.gridCard}>
            <Text style={styles.gridIcon}>🌐</Text>
            <Text style={styles.gridTitle}>Cross-Platform</Text>
            <Text style={styles.gridDesc}>Tek kod tabanı ile Web tarayıcıları ve Mobil cihazlar için %100 responsive tasarım.</Text>
          </View>
          <View style={styles.gridCard}>
            <Text style={styles.gridIcon}>⚡</Text>
            <Text style={styles.gridTitle}>Gerçek Zamanlı</Text>
            <Text style={styles.gridDesc}>GitHub Actions ve ArgoCD boru hattı durumlarını canlı takip imkanı.</Text>
          </View>
        </View>

        {/* Footer */}
        <View style={styles.footer}>
          <Text style={styles.footerText}>Datactive GitOps Engine v1.0.0 — K1 (Web/Backend Team)</Text>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0f172a',
  },
  scrollContent: {
    padding: 20,
    alignItems: 'center',
  },
  header: {
    alignItems: 'center',
    marginVertical: 30,
  },
  logoText: {
    fontSize: 32,
    fontWeight: 'bold',
    color: '#ffffff',
    letterSpacing: 1,
  },
  logoAccent: {
    color: '#3b82f6',
  },
  subtitleText: {
    fontSize: 14,
    color: '#94a3b8',
    marginTop: 8,
    textAlign: 'center',
  },
  card: {
    width: '100%',
    maxWidth: 700,
    backgroundColor: '#1e293b',
    borderRadius: 16,
    padding: 24,
    borderWidth: 1,
    borderColor: '#334155',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
    marginBottom: 24,
  },
  cardMobile: {
    padding: 16,
  },
  cardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#334155',
    paddingBottom: 12,
  },
  cardTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#f8fafc',
  },
  refreshButton: {
    backgroundColor: '#3b82f6',
    paddingHorizontal: 14,
    paddingVertical: 8,
    borderRadius: 8,
  },
  refreshButtonText: {
    color: '#ffffff',
    fontWeight: '600',
    fontSize: 13,
  },
  statusRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginVertical: 10,
  },
  label: {
    fontSize: 14,
    color: '#94a3b8',
  },
  urlValue: {
    fontSize: 14,
    color: '#38bdf8',
    fontFamily: 'monospace',
  },
  badge: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 20,
  },
  badgeSuccess: {
    backgroundColor: '#064e3b',
  },
  badgeError: {
    backgroundColor: '#7f1d1d',
  },
  badgeText: {
    color: '#ffffff',
    fontWeight: '600',
    fontSize: 13,
  },
  loadingText: {
    color: '#94a3b8',
    fontSize: 13,
  },
  infoBox: {
    backgroundColor: '#0f172a',
    borderRadius: 8,
    padding: 12,
    marginTop: 16,
    borderLeftWidth: 4,
    borderLeftColor: '#3b82f6',
  },
  infoBoxError: {
    borderLeftColor: '#ef4444',
  },
  infoText: {
    color: '#cbd5e1',
    fontSize: 13,
    marginVertical: 2,
  },
  boldText: {
    fontWeight: 'bold',
    color: '#f8fafc',
  },
  errorText: {
    color: '#fca5a5',
    fontSize: 13,
  },
  grid: {
    flexDirection: 'row',
    width: '100%',
    maxWidth: 700,
    justifyContent: 'space-between',
    gap: 16,
  },
  gridMobile: {
    flexDirection: 'column',
  },
  gridCard: {
    flex: 1,
    backgroundColor: '#1e293b',
    borderRadius: 12,
    padding: 20,
    borderWidth: 1,
    borderColor: '#334155',
  },
  gridIcon: {
    fontSize: 24,
    marginBottom: 8,
  },
  gridTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#f8fafc',
    marginBottom: 6,
  },
  gridDesc: {
    fontSize: 13,
    color: '#94a3b8',
    lineHeight: 18,
  },
  footer: {
    marginTop: 40,
    marginBottom: 20,
  },
  footerText: {
    fontSize: 12,
    color: '#64748b',
  },
});
