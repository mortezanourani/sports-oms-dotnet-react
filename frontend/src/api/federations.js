import api from './axios';

export const getFederations = async (params) => (await api.get('/federations', { params })).data;
export const getFederation = async (id) => (await api.get(`/federations/${id}`)).data;
export const createFederation = async (payload) => (await api.post('/federations', payload)).data;
export const importFederations = async (file) => {
    const formData = new FormData();
    formData.append('file', file);
    return (await api.post('/federations/import', formData)).data;
};
export const downloadFederationsImportTemplate = async () =>
    (await api.get('/federations/import/template', { responseType: 'blob' })).data;
export const updateFederation = async ({ id, payload }) => (await api.put(`/federations/${id}`, payload)).data;
export const deleteFederation = async (id) => { await api.delete(`/federations/${id}`); };
export const getFederationPositions = async (id) => (await api.get(`/federations/${id}/positions`)).data;
export const createFederationRep = async ({ federationId, payload }) =>
  (await api.post(`/federations/${federationId}/federation-reps`, payload)).data;
