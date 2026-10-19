import api from './axios';

export const getChampions = async (params) => {
    const { data } = await api.get('/champions', { params });
    return data;
};

export const getChampionFederations = async () => {
    const { data } = await api.get('/champions/federations');
    return data;
};

export const getChampion = async (id) => {
    const { data } = await api.get(`/champions/${id}`);
    return data;
};

export const createChampion = async (payload) => {
    const { data } = await api.post('/champions', payload);
    return data;
};

export const updateChampion = async ({ id, payload }) => {
    const { data } = await api.put(`/champions/${id}`, payload);
    return data;
};

export const deleteChampion = async (id) => {
    await api.delete(`/champions/${id}`);
};

export const addMedal = async ({ championId, payload }) => {
    const { data } = await api.post(`/champions/${championId}/medals`, payload);
    return data;
};

export const deleteMedal = async ({ championId, medalId }) => {
    await api.delete(`/champions/${championId}/medals/${medalId}`);
};

export const importChampions = async (file) => {
    const formData = new FormData();
    formData.append('file', file);
    return (await api.post('/champions/import', formData)).data;
};

export const downloadChampionsImportTemplate = async () =>
    (await api.get('/champions/import/template', { responseType: 'blob' })).data;
