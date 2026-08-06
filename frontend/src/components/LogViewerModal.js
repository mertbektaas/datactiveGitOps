import React, { useState, useEffect, useRef } from 'react';
import { StyleSheet, Text, View, Modal, TouchableOpacity, ScrollView, ActivityIndicator } from 'react-native';
import { getBuildLogs } from '../services/api';

export default function LogViewerModal({ visible, buildId, tag, onClose }) {
  const [logs, setLogs] = useState('');
  const [loading, setLoading] = useState(true);
  const [copied, setCopied] = useState(false);
  const scrollViewRef = useRef(null);

  useEffect(() => {
    if (!visible || !buildId) return;

    const fetchLogs = async () => {
      setLoading(true);
      setCopied(false);
      try {
        const data = await getBuildLogs(buildId);
        setLogs(data.logs || 'Log kaydı bulunamadı.');
      } catch (err) {
        setLogs(`⚠️ Log akışı çekilirken hata oluştu: ${err.message}`);
      } finally {
        setLoading(false);
      }
    };

    fetchLogs();
  }, [visible, buildId]);

  const handleCopy = () => {
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <Modal visible={visible} animationType="slide" transparent={true} onRequestClose={onClose}>
      <View style={styles.modalOverlay}>
        <View style={styles.terminalContainer}>
          {/* Terminal Window Header Bar */}
          <View style={styles.terminalHeader}>
            <View style={styles.windowControls}>
              <View style={[styles.dot, styles.dotClose]} />
              <View style={[styles.dot, styles.dotMin]} />
              <View style={[styles.dot, styles.dotMax]} />
            </View>

            <Text style={styles.terminalTitle}>
              📟 Terminal Log Stream — Build #{buildId} {tag ? `(${tag})` : ''}
            </Text>

            <View style={styles.headerActions}>
              <TouchableOpacity style={styles.copyBtn} onPress={handleCopy}>
                <Text style={styles.copyBtnText}>{copied ? '✓ Kopyalandı' : '📋 Kopyala'}</Text>
              </TouchableOpacity>
              <TouchableOpacity style={styles.closeBtn} onPress={onClose}>
                <Text style={styles.closeBtnText}>✖ Kapat</Text>
              </TouchableOpacity>
            </View>
          </View>

          {/* Terminal Console View */}
          {loading ? (
            <View style={styles.loadingContainer}>
              <ActivityIndicator size="large" color="#38bdf8" />
              <Text style={styles.loadingText}>GitHub Actions canlı konsol logları çekiliyor...</Text>
            </View>
          ) : (
            <ScrollView
              ref={scrollViewRef}
              style={styles.consoleBody}
              contentContainerStyle={styles.consoleContent}
              onContentSizeChange={() => scrollViewRef.current?.scrollToEnd({ animated: true })}
            >
              <Text style={styles.logText}>{logs}</Text>
            </ScrollView>
          )}
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.75)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  terminalContainer: {
    width: '100%',
    maxWidth: 900,
    height: '80%',
    backgroundColor: '#090d16',
    borderRadius: 12,
    borderWidth: 1,
    borderColor: '#1e293b',
    overflow: 'hidden',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 10 },
    shadowOpacity: 0.5,
    shadowRadius: 15,
  },
  terminalHeader: {
    height: 44,
    backgroundColor: '#1e293b',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#334155',
  },
  windowControls: {
    flexDirection: 'row',
    gap: 6,
  },
  dot: {
    width: 12,
    height: 12,
    borderRadius: 6,
  },
  dotClose: {
    backgroundColor: '#ef4444',
  },
  dotMin: {
    backgroundColor: '#eab308',
  },
  dotMax: {
    backgroundColor: '#22c55e',
  },
  terminalTitle: {
    color: '#f8fafc',
    fontSize: 13,
    fontWeight: '600',
    fontFamily: 'monospace',
  },
  headerActions: {
    flexDirection: 'row',
    gap: 8,
  },
  copyBtn: {
    backgroundColor: '#334155',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 6,
  },
  copyBtnText: {
    color: '#38bdf8',
    fontSize: 12,
    fontWeight: 'bold',
  },
  closeBtn: {
    backgroundColor: '#991b1b',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 6,
  },
  closeBtnText: {
    color: '#ffffff',
    fontSize: 12,
    fontWeight: 'bold',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    color: '#94a3b8',
    marginTop: 12,
    fontSize: 13,
  },
  consoleBody: {
    flex: 1,
    backgroundColor: '#090d16',
    padding: 16,
  },
  consoleContent: {
    paddingBottom: 24,
  },
  logText: {
    color: '#22c55e',
    fontSize: 13,
    fontFamily: 'monospace',
    lineHeight: 20,
  },
});
