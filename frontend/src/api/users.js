import api from './axios';

export const getUsers = async (params) => (await api.get('/users', { params })).data;
export const getUser = async (id) => (await api.get(`/users/${id}`)).data;
export const getUserByUsername = async (username) => (await api.get(`/users/username/${username}`)).data;
export const createUser = async (payload) => (await api.post('/users', payload)).data;
export const importUsers = async (file) => {
  const formData = new FormData();
  formData.append('file', file);
  return (await api.post('/users/import', formData)).data;
};
export const downloadUsersImportTemplate = async () =>
  (await api.get('/users/import/template', { responseType: 'blob' })).data;
export const updateUser = async ({ id, payload }) => (await api.put(`/users/${id}`, payload)).data;
export const deactivateUser = async (id) => { await api.delete(`/users/${id}`); };
export const assignFederationPosition = async ({ userId, payload }) =>
  (await api.post(`/users/${userId}/federation-position`, payload)).data;
export const getUserFederationPositions = async (userId) =>
  (await api.get(`/users/${userId}/federation-positions`)).data;
