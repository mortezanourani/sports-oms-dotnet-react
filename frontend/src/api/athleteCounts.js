import api from './axios';

export const getAthleteCounts = async (params) => (await api.get('/athlete-counts', { params })).data;
export const getAthleteCount = async (id) => (await api.get(`/athlete-counts/${id}`)).data;
export const createAthleteCount = async (payload) => (await api.post('/athlete-counts', payload)).data;
export const updateAthleteCount = async ({ id, payload }) => (await api.put(`/athlete-counts/${id}`, payload)).data;
export const deleteAthleteCount = async (id) => { await api.delete(`/athlete-counts/${id}`); };
