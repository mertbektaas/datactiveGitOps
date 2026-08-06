import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, ActivityIndicator, TouchableOpacity } from 'react-native';
import { getBranches } from '../services/api';

const TICKET_REGEX = /(?:[A-Za-z]+\d+-\d+|K\d+-\d+|C\d+-\d+|\b\d{4,}\b)/i;

export default function BranchSelector({ onSelectPair }) {
  const [webBranches, setWebBranches] = useState([]);
  const [serverBranches, setServerBranches] = useState([]);
  const [selectedWeb, setSelectedWeb] = useState('');
  const [selectedServer, setSelectedServer] = useState('');
  const [isAutoMatched, setIsAutoMatched] = useState(false);
  const [matchedTicket, setMatchedTicket] = useState(null);
  
  const [loadingWeb, setLoadingWeb] = useState(true);
  const [loadingServer, setLoadingServer] = useState(true);
  const [error, setError] = useState(null);

  const fetchAllBranches = async () => {
    setError(null);
    setLoadingWeb(true);
    setLoadingServer(true);

    try {
      const [webData, serverData] = await Promise.all([
        getBranches('web'),
        getBranches('server')
      ]);

      setWebBranches(webData || []);
      setServerBranches(serverData || []);

      // Default web selection: main/master or first branch
      const defaultWeb = webData.find(b => b.isDefault)?.name || webData[0]?.name || 'main';
      setSelectedWeb(defaultWeb);
    } catch (err) {
      setError('Branch verileri yüklenirken hata oluştu. Backend API servisini kontrol edin.');
    } finally {
      setLoadingWeb(false);
      setLoadingServer(false);
    }
  };

  useEffect(() => {
    fetchAllBranches();
  }, []);

  // Smart Matching Logic
  useEffect(() => {
    if (!selectedWeb || serverBranches.length === 0) return;

    const ticketMatch = selectedWeb.match(TICKET_REGEX);
    const ticket = ticketMatch ? ticketMatch[0].toUpperCase() : null;
    setMatchedTicket(ticket);

    if (ticket) {
      // Find server branch containing the same ticket
      const matchedServerBranch = serverBranches.find(b => 
        b.name.toUpperCase().includes(ticket)
      );

      if (matchedServerBranch) {
        setSelectedServer(matchedServerBranch.name);
        setIsAutoMatched(true);
        if (onSelectPair) {
          onSelectPair({ branchWeb: selectedWeb, branchServer: matchedServerBranch.name, ticket, isAutoMatched: true });
        }
        return;
      }
    }

    // Fallback: Default server branch (main) or current selected
    const fallbackServer = serverBranches.find(b => b.isDefault)?.name || serverBranches[0]?.name || 'main';
    setSelectedServer(fallbackServer);
    setIsAutoMatched(false);

    if (onSelectPair) {
      onSelectPair({ branchWeb: selectedWeb, branchServer: fallbackServer, ticket: null, isAutoMatched: false });
    }
  }, [selectedWeb, serverBranches]);

  const handleManualServerSelect = (branchName) => {
    setSelectedServer(branchName);
    setIsAutoMatched(false);
    if (onSelectPair) {
      onSelectPair({ branchWeb: selectedWeb, branchServer: branchName, ticket: matchedTicket, isAutoMatched: false });
    }
  };

  if (loadingWeb || loadingServer) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#3b82f6" />
        <Text style={styles.loadingText}>GitHub dalları (branches) yükleniyor...</Text>
      </View>
    );
  }

  if (error) {
    return (
      <View style={styles.errorBox}>
        <Text style={styles.errorText}>⚠️ {error}</Text>
        <TouchableOpacity style={styles.retryButton} onPress={fetchAllBranches}>
          <Text style={styles.retryButtonText}>Tekrar Deneyin</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <Text style={styles.sectionTitle}>1. Depo Dal (Branch) Seçimi ve Eşleştirme</Text>

      {/* Web Branch Selection */}
      <View style={styles.fieldGroup}>
        <Text style={styles.label}>🌐 Web Repository Branch (datateam-web):</Text>
        <View style={styles.chipContainer}>
          {webBranches.map((b) => (
            <TouchableOpacity
              key={b.name}
              style={[styles.chip, selectedWeb === b.name && styles.chipActive]}
              onPress={() => setSelectedWeb(b.name)}
            >
              <Text style={[styles.chipText, selectedWeb === b.name && styles.chipTextActive]}>
                {b.name} {b.isDefault ? '⭐' : ''}
              </Text>
            </TouchableOpacity>
          ))}
        </View>
      </View>

      {/* Server Branch Selection */}
      <View style={styles.fieldGroup}>
        <View style={styles.labelRow}>
          <Text style={styles.label}>🖥️ Server Repository Branch (datateam-core.server):</Text>
          {isAutoMatched ? (
            <View style={styles.autoBadge}>
              <Text style={styles.autoBadgeText}>⚡ Akıllı Eşleşme ({matchedTicket})</Text>
            </View>
          ) : (
            <View style={styles.manualBadge}>
              <Text style={styles.manualBadgeText}>🖐️ Manuel/Varsayılan</Text>
            </View>
          )}
        </View>

        <View style={styles.chipContainer}>
          {serverBranches.map((b) => (
            <TouchableOpacity
              key={b.name}
              style={[styles.chip, selectedServer === b.name && styles.chipActiveServer]}
              onPress={() => handleManualServerSelect(b.name)}
            >
              <Text style={[styles.chipText, selectedServer === b.name && styles.chipTextActive]}>
                {b.name} {b.isDefault ? '⭐' : ''}
              </Text>
            </TouchableOpacity>
          ))}
        </View>
      </View>

      {/* Selected Pair Confirmation Card */}
      <View style={styles.summaryCard}>
        <Text style={styles.summaryTitle}>Seçili Dal Çifti Onayı:</Text>
        <Text style={styles.summaryText}>
          <Text style={styles.bold}>Web:</Text> {selectedWeb} ➔ <Text style={styles.bold}>Server:</Text> {selectedServer}
        </Text>
      </View>
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
  labelRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
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
    backgroundColor: '#1d4ed8',
    borderColor: '#3b82f6',
  },
  chipActiveServer: {
    backgroundColor: '#047857',
    borderColor: '#10b981',
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
  autoBadge: {
    backgroundColor: '#064e3b',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
  },
  autoBadgeText: {
    color: '#34d399',
    fontSize: 11,
    fontWeight: 'bold',
  },
  manualBadge: {
    backgroundColor: '#334155',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
  },
  manualBadgeText: {
    color: '#94a3b8',
    fontSize: 11,
  },
  summaryCard: {
    backgroundColor: '#0f172a',
    borderRadius: 10,
    padding: 12,
    marginTop: 8,
    borderLeftWidth: 4,
    borderLeftColor: '#10b981',
  },
  summaryTitle: {
    fontSize: 12,
    color: '#94a3b8',
    marginBottom: 4,
  },
  summaryText: {
    fontSize: 14,
    color: '#f8fafc',
  },
  bold: {
    fontWeight: 'bold',
    color: '#38bdf8',
  },
  loadingContainer: {
    padding: 24,
    alignItems: 'center',
    backgroundColor: '#1e293b',
    borderRadius: 16,
    marginBottom: 20,
  },
  loadingText: {
    color: '#94a3b8',
    marginTop: 10,
    fontSize: 13,
  },
  errorBox: {
    backgroundColor: '#7f1d1d',
    borderRadius: 12,
    padding: 16,
    marginBottom: 20,
  },
  errorText: {
    color: '#fca5a5',
    fontSize: 13,
    marginBottom: 10,
  },
  retryButton: {
    backgroundColor: '#991b1b',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 6,
    alignSelf: 'flex-start',
  },
  retryButtonText: {
    color: '#ffffff',
    fontWeight: 'bold',
    fontSize: 12,
  },
});
