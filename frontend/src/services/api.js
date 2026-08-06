import axios from 'axios';

const API_BASE_URL = process.env.EXPO_PUBLIC_API_URL || 'http://localhost:8080';
const API_KEY = process.env.EXPO_PUBLIC_API_KEY || 'datactive-gitops-secret-key-2026';

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
    'X-API-KEY': API_KEY,
  },
});

export const getBranches = async (repoType = 'web') => {
  try {
    const response = await apiClient.get(`/api/branches?repo=${repoType}`);
    return response.data;
  } catch (error) {
    console.error(`Error fetching branches for ${repoType}:`, error);
    throw error;
  }
};

export const getSchemas = async () => {
  try {
    const response = await apiClient.get('/api/schemas');
    return response.data;
  } catch (error) {
    console.error('Error fetching schemas:', error);
    return [
      { id: 'schema_dev', name: 'schema_dev (Geliştirme / Test)' },
      { id: 'schema_k1_5', name: 'schema_k1_5 (K1-5 Bilet Şeması)' },
      { id: 'datactive_mix_tenant', name: 'datactive_mix_tenant (Tenant Izole)' }
    ];
  }
};

export const generateTag = async (branchWeb, branchServer) => {
  try {
    const response = await apiClient.get(`/api/tags/generate?branchWeb=${encodeURIComponent(branchWeb)}&branchServer=${encodeURIComponent(branchServer || '')}`);
    return response.data.tag;
  } catch (error) {
    console.error('Error generating tag:', error);
    return null;
  }
};

export const dispatchBuild = async (buildData) => {
  try {
    const response = await apiClient.post('/api/builds', buildData);
    return response.data;
  } catch (error) {
    console.error('Error dispatching build:', error);
    throw error;
  }
};

export const getBuilds = async (search = '') => {
  try {
    const url = search ? `/api/builds?search=${encodeURIComponent(search)}` : '/api/builds';
    const response = await apiClient.get(url);
    return response.data;
  } catch (error) {
    console.error('Error fetching builds list:', error);
    return [];
  }
};

export const getBuildStatus = async (buildId) => {
  try {
    const response = await apiClient.get(`/api/builds/${buildId}`);
    return response.data;
  } catch (error) {
    console.error(`Error fetching build status for ${buildId}:`, error);
    throw error;
  }
};

export const getBuildLogs = async (buildId) => {
  try {
    const response = await apiClient.get(`/api/builds/${buildId}/logs`);
    return response.data;
  } catch (error) {
    console.error(`Error fetching logs for ${buildId}:`, error);
    throw error;
  }
};

export const triggerArgoCdSync = async (appName) => {
  try {
    const response = await apiClient.post(`/api/argocd/sync/${appName}`);
    return response.data;
  } catch (error) {
    console.error(`Error triggering ArgoCD sync for ${appName}:`, error);
    throw error;
  }
};

export default apiClient;
