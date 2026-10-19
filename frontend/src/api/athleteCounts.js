import api from './axios';

export const getAthleteCounts = async (params) => (await api.get('/athlete-counts', { params })).data;
export const getAthleteCountSummary = async (params) => (await api.get('/athlete-counts/summary', { params })).data;
export const getAthleteCountSummaryFilters = async (params) => (await api.get('/athlete-counts/summary/filters', { params })).data;
export const getAthleteCount = async (id) => (await api.get(`/athlete-counts/${id}`)).data;
export const createAthleteCount = async (payload) => (await api.post('/athlete-counts', payload)).data;
export const importAthleteCounts = async (file) => {
    const formData = new FormData();
    formData.append('file', file);
    return (await api.post('/athlete-counts/import', formData)).data;
};
export const downloadAthleteCountsImportTemplate = async () =>
    (await api.get('/athlete-counts/import/template', { responseType: 'blob' })).data;
export const updateAthleteCount = async ({ id, payload }) => (await api.put(`/athlete-counts/${id}`, payload)).data;
export const deleteAthleteCount = async (id) => { await api.delete(`/athlete-counts/${id}`); };
