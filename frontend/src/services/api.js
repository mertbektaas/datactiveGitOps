import axios from 'axios';

const API_BASE_URL = process.env.EXPO_PUBLIC_API_URL || 'http://localhost:8080';

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
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

export const getBuildStatus = async (buildId) => {
  try {
    const response = await apiClient.get(`/api/builds/${buildId}`);
    return response.data;
  } catch (error) {
    console.error(`Error fetching build status for ${buildId}:`, error);
    throw error;
  }
};

export default apiClient;
