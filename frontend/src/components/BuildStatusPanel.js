import React, { useState, useEffect, useCallback } from 'react';
import { StyleSheet, Text, View, TextInput, TouchableOpacity, ActivityIndicator } from 'react-native';
import { getBuilds, getBuildStatus, triggerArgoCdSync } from '../services/api';
import LogViewerModal from './LogViewerModal';

export default function BuildStatusPanel({ latestBuildTrigger }) {
  const [builds, setBuilds] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const [syncingMap, setSyncingMap] = useState({});
  const [syncNotice, setSyncNotice] = useState(null);

  const [activeLogModal, setActiveLogModal] = useState({
    visible: false,
    buildId: null,
    tag: null,
  });

  const fetchBuildsList = useCallback(async (query = searchQuery) => {
    try {
      const data = await getBuilds(query);
      setBuilds(data || []);

      // On-demand refresh for any active builds (queued or in_progress)
      const activeBuilds = (data || []).filter(
        (b) => b.status === 'queued' || b.status === 'in_progress'
      );

      if (activeBuilds.length > 0) {
        await Promise.all(
          activeBuilds.map(async (b) => {
            try {
              const updated = await getBuildStatus(b.buildId);
              if (updated && updated.status !== b.status) {
                setBuilds((prev) =>
                  prev.map((item) =>
                    item.buildId === b.buildId ? { ...item, status: updated.status } : item
                  )
                );
              }
            } catch (err) {
              console.warn(`Poller status refresh error for build ${b.buildId}:`, err);
            }
          })
        );
      }
    } catch (err) {
      console.error('Error fetching builds history:', err);
    } finally {
      setLoading(false);
      setIsRefreshing(false);
    }
  }, [searchQuery]);

  useEffect(() => {
    fetchBuildsList();
  }, [fetchBuildsList, latestBuildTrigger]);

  // Handle Search Input Change
  const handleSearchChange = (text) => {
    setSearchQuery(text);
    fetchBuildsList(text);
  };

  // Polling loop: Refresh active builds every 5 seconds
  useEffect(() => {
    const hasActiveBuilds = builds.some(
      (b) => b.status === 'queued' || b.status === 'in_progress'
    );

    if (!hasActiveBuilds) return;

    const intervalId = setInterval(() => {
      fetchBuildsList();
    }, 5000);

    return () => clearInterval(intervalId);
  }, [builds, fetchBuildsList]);

  const handleManualRefresh = () => {
    setIsRefreshing(true);
    fetchBuildsList();
  };

  const handleReSyncArgoCd = async (ns) => {
    const appName = `app-${ns || 'build-test'}`;
    setSyncingMap((prev) => ({ ...prev, [appName]: true }));
    setSyncNotice(null);

    try {
      const result = await triggerArgoCdSync(appName);
      setSyncNotice(`✅ ${appName} için ArgoCD Sync sinyali gönderildi! (${result.status})`);
    } catch (err) {
      setSyncNotice(`⚠️ Sync hatası: ${err.message}`);
    } finally {
      setSyncingMap((prev) => ({ ...prev, [appName]: false }));
    }
  };

  const getStatusBadge = (status) => {
    const s = (status || '').toLowerCase();

    if (s === 'queued') {
      return (
        <View style={[styles.badge, styles.badgeQueued]}>
          <Text style={styles.badgeText}>● Sıraya Alındı (Queued)</Text>
        </View>
      );
    }

    if (s === 'in_progress') {
      return (
        <View style={[styles.badge, styles.badgeInProgress]}>
          <ActivityIndicator size="small" color="#ffffff" style={{ marginRight: 4 }} />
          <Text style={styles.badgeText}>İşleniyor...</Text>
        </View>
      );
    }

    if (s === 'success') {
      return (
        <View style={[styles.badge, styles.badgeSuccess]}>
          <Text style={styles.badgeText}>✔ Başarılı (Success)</Text>
        </View>
      );
    }

    return (
      <View style={[styles.badge, styles.badgeFailure]}>
        <Text style={styles.badgeText}>✖ Başarısız ({status})</Text>
      </View>
    );
  };

  return (
    <View style={styles.container}>
      <View style={styles.headerRow}>
        <Text style={styles.sectionTitle}>3. Canlı Build & ArgoCD Dağıtım Paneli</Text>
        <TouchableOpacity
          style={styles.refreshBtn}
          onPress={handleManualRefresh}
          disabled={isRefreshing}
        >
          {isRefreshing ? (
            <ActivityIndicator size="small" color="#ffffff" />
          ) : (
            <Text style={styles.refreshBtnText}>🔄 Yenile</Text>
          )}
        </TouchableOpacity>
      </View>

      {/* Search Input Filter */}
      <View style={styles.searchRow}>
        <TextInput
          style={styles.searchInput}
          placeholder="🔍 Tag, Bilet (K1-5), Namespace veya Şema ile geçmişte ara..."
          placeholderTextColor="#64748b"
          value={searchQuery}
          onChangeText={handleSearchChange}
        />
      </View>

      {syncNotice && (
        <View style={styles.noticeBox}>
          <Text style={styles.noticeText}>{syncNotice}</Text>
        </View>
      )}

      {loading ? (
        <View style={styles.loadingBox}>
          <ActivityIndicator size="large" color="#3b82f6" />
          <Text style={styles.loadingText}>Derleme geçmişi yükleniyor...</Text>
        </View>
      ) : builds.length === 0 ? (
        <View style={styles.emptyBox}>
          <Text style={styles.emptyText}>Aranan kriterlere uygun kaydolmuş bir build bulunamadı.</Text>
        </View>
      ) : (
        <View style={styles.listContainer}>
          {builds.map((item) => {
            const ns = item.namespaceName || item.namespace;
            const appName = `app-${ns}`;
            const isSyncing = syncingMap[appName];

            return (
              <View key={item.buildId} style={styles.buildCard}>
                <View style={styles.cardTopRow}>
                  <View style={styles.idContainer}>
                    <Text style={styles.buildIdText}>Build #{item.buildId}</Text>
                    <Text style={styles.tagText}>{item.tag}</Text>
                  </View>
                  {getStatusBadge(item.status)}
                </View>

                <View style={styles.cardDetails}>
                  <Text style={styles.detailText}>
                    <Text style={styles.bold}>Namespace:</Text> {ns}
                  </Text>
                  <Text style={styles.detailText}>
                    <Text style={styles.bold}>Branchler:</Text> Web ({item.branchWeb}) | Server ({item.branchServer})
                  </Text>
                  <Text style={styles.detailText}>
                    <Text style={styles.bold}>Şema:</Text> {item.schema}
                  </Text>
                  {item.commitSha && (
                    <Text style={styles.detailText}>
                      <Text style={styles.bold}>Commit SHA:</Text> {item.commitSha.substring(0, 7)}
                    </Text>
                  )}

                  <View style={styles.cardActionRow}>
                    <Text style={styles.timeText}>
                      {new Date(item.createdAt).toLocaleString('tr-TR')}
                    </Text>

                    <View style={styles.btnGroup}>
                      <TouchableOpacity
                        style={styles.logsBtn}
                        onPress={() =>
                          setActiveLogModal({
                            visible: true,
                            buildId: item.buildId,
                            tag: item.tag,
                          })
                        }
                      >
                        <Text style={styles.logsBtnText}>📄 Logları İncele</Text>
                      </TouchableOpacity>

                      <TouchableOpacity
                        style={[styles.syncBtn, isSyncing && styles.syncBtnDisabled]}
                        onPress={() => handleReSyncArgoCd(ns)}
                        disabled={isSyncing}
                      >
                        {isSyncing ? (
                          <ActivityIndicator size="small" color="#ffffff" />
                        ) : (
                          <Text style={styles.syncBtnText}>⚡ ArgoCD Re-Sync</Text>
                        )}
                      </TouchableOpacity>
                    </View>
                  </View>
                </View>
              </View>
            );
          })}
        </View>
      )}

      {/* Log Terminal Viewer Modal */}
      <LogViewerModal
        visible={activeLogModal.visible}
        buildId={activeLogModal.buildId}
        tag={activeLogModal.tag}
        onClose={() => setActiveLogModal({ visible: false, buildId: null, tag: null })}
      />
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
    marginBottom: 24,
  },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#334155',
    paddingBottom: 8,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#f8fafc',
  },
  refreshBtn: {
    backgroundColor: '#3b82f6',
    paddingHorizontal: 10,
    paddingVertical: 5,
    borderRadius: 6,
  },
  refreshBtnText: {
    color: '#ffffff',
    fontSize: 12,
    fontWeight: '600',
  },
  searchRow: {
    marginBottom: 14,
  },
  searchInput: {
    backgroundColor: '#0f172a',
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
    color: '#f8fafc',
    fontSize: 13,
    borderWidth: 1,
    borderColor: '#334155',
  },
  noticeBox: {
    backgroundColor: '#0284c7',
    borderRadius: 8,
    padding: 10,
    marginBottom: 14,
  },
  noticeText: {
    color: '#ffffff',
    fontSize: 12,
    fontWeight: '600',
  },
  loadingBox: {
    padding: 30,
    alignItems: 'center',
  },
  loadingText: {
    color: '#94a3b8',
    marginTop: 10,
    fontSize: 13,
  },
  emptyBox: {
    padding: 20,
    alignItems: 'center',
  },
  emptyText: {
    color: '#94a3b8',
    fontSize: 13,
  },
  listContainer: {
    gap: 12,
  },
  buildCard: {
    backgroundColor: '#0f172a',
    borderRadius: 12,
    padding: 16,
    borderWidth: 1,
    borderColor: '#334155',
  },
  cardTopRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 10,
  },
  idContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  buildIdText: {
    fontSize: 15,
    fontWeight: 'bold',
    color: '#f8fafc',
  },
  tagText: {
    fontSize: 12,
    color: '#38bdf8',
    fontFamily: 'monospace',
    backgroundColor: '#1e293b',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 4,
  },
  badge: {
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 20,
    flexDirection: 'row',
    alignItems: 'center',
  },
  badgeQueued: {
    backgroundColor: '#854d0e',
  },
  badgeInProgress: {
    backgroundColor: '#1d4ed8',
  },
  badgeSuccess: {
    backgroundColor: '#064e3b',
  },
  badgeFailure: {
    backgroundColor: '#7f1d1d',
  },
  badgeText: {
    color: '#ffffff',
    fontSize: 11,
    fontWeight: 'bold',
  },
  cardDetails: {
    gap: 4,
    borderTopWidth: 1,
    borderTopColor: '#1e293b',
    paddingTop: 8,
  },
  detailText: {
    color: '#cbd5e1',
    fontSize: 13,
  },
  bold: {
    fontWeight: 'bold',
    color: '#94a3b8',
  },
  cardActionRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: 6,
  },
  timeText: {
    fontSize: 11,
    color: '#64748b',
  },
  btnGroup: {
    flexDirection: 'row',
    gap: 8,
  },
  logsBtn: {
    backgroundColor: '#334155',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 6,
  },
  logsBtnText: {
    color: '#38bdf8',
    fontSize: 11,
    fontWeight: 'bold',
  },
  syncBtn: {
    backgroundColor: '#059669',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 6,
  },
  syncBtnDisabled: {
    backgroundColor: '#374151',
  },
  syncBtnText: {
    color: '#ffffff',
    fontSize: 11,
    fontWeight: 'bold',
  },
});
