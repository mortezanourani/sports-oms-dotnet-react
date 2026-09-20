import api from './axios';

export const getFederations = async () => (await api.get('/federations')).data;
export const getFederation = async (id) => (await api.get(`/federations/${id}`)).data;
export const createFederation = async (payload) => (await api.post('/federations', payload)).data;
export const updateFederation = async ({ id, payload }) => (await api.put(`/federations/${id}`, payload)).data;
export const deleteFederation = async (id) => { await api.delete(`/federations/${id}`); };
export const getFederationPositions = async (id) => (await api.get(`/federations/${id}/positions`)).data;
