import api from './axios';

export const getJobPostTypes = async () => (await api.get('/job-post-types')).data;

export const createJobPostType = async (payload) =>
    (await api.post('/job-post-types', payload)).data;

export const updateJobPostType = async ({ id, payload }) =>
    (await api.put(`/job-post-types/${id}`, payload)).data;

export const importJobPostTypes = async (file) => {
    const formData = new FormData();
    formData.append('file', file);
    return (await api.post('/job-post-types/import', formData)).data;
};

export const downloadJobPostTypesImportTemplate = async () =>
    (await api.get('/job-post-types/import/template', { responseType: 'blob' })).data;
