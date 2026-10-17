import api from './axios';

export const getFacilities = async (params) => {
    const { data } = await api.get('/facilities', { params });
    return data;
};

export const getFacility = async (id) => {
    const { data } = await api.get(`/facilities/${id}`);
    return data;
};

export const createFacility = async (payload) => {
    const { data } = await api.post('/facilities', payload);
    return data;
};

export const updateFacility = async ({ id, payload }) => {
    const { data } = await api.put(`/facilities/${id}`, payload);
    return data;
};

export const deleteFacility = async (id) => {
    await api.delete(`/facilities/${id}`);
};

export const importPrivateFacilities = async (file) => {
    const formData = new FormData();
    formData.append('file', file);
    return (await api.post('/facilities/import', formData)).data;
};

export const downloadPrivateFacilitiesImportTemplate = async () =>
    (await api.get('/facilities/import/template', { responseType: 'blob' })).data;
