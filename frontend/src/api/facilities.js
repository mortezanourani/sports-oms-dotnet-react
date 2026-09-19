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
