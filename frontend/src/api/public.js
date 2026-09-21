import api from './axios';

export const getPublicFacilityStats = async (params) => (await api.get('/public/facilities/stats', { params })).data;
export const getPublicChampions = async (params) => (await api.get('/public/champions', { params })).data;
export const getPublicAthleteStats = async (params) => (await api.get('/public/athletes/stats', { params })).data;
export const getPublicFederations = async () => (await api.get('/public/federations')).data;
export const getPublicProvinces = async () => (await api.get('/public/provinces')).data;
