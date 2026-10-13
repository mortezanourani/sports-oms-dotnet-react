import api from './axios';

export const getProvinces = async () => {
    const { data } = await api.get('/locations/provinces');
    return data;
};

export const importLocations = async (file) => {
    const formData = new FormData();
    formData.append('file', file);
    return (await api.post('/locations/import', formData)).data;
};

export const downloadLocationsImportTemplate = async () =>
    (await api.get('/locations/import/template', { responseType: 'blob' })).data;

export const createProvince = async (payload) => {
    const { data } = await api.post('/locations/provinces', payload);
    return data;
};

export const updateProvince = async ({ id, payload }) => {
    const { data } = await api.put(`/locations/provinces/${id}`, payload);
    return data;
};

export const deleteProvince = async (id) => {
    await api.delete(`/locations/provinces/${id}`);
};

export const getCities = async (provinceId) => {
    const { data } = await api.get('/locations/cities', {
        params: provinceId ? { provinceId } : {},
    });
    return data;
};

export const createCity = async (payload) => {
    const { data } = await api.post('/locations/cities', payload);
    return data;
};

export const updateCity = async ({ id, payload }) => {
    const { data } = await api.put(`/locations/cities/${id}`, payload);
    return data;
};

export const deleteCity = async (id) => {
    await api.delete(`/locations/cities/${id}`);
};
