import api from './axios';

export const getOrgUnits = async () => (await api.get('/org-units')).data;

export const createOrgUnit = async (payload) =>
    (await api.post('/org-units', payload)).data;

export const updateOrgUnit = async ({ id, payload }) =>
    (await api.put(`/org-units/${id}`, payload)).data;

export const importOrgUnits = async (file) => {
    const formData = new FormData();
    formData.append('file', file);
    return (await api.post('/org-units/import', formData)).data;
};

export const downloadOrgUnitsImportTemplate = async () =>
    (await api.get('/org-units/import/template', { responseType: 'blob' })).data;
