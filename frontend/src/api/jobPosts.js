import api from './axios';

export const getJobPosts = async () => (await api.get('/job-posts')).data;

export const createJobPost = async (payload) =>
  (await api.post('/job-posts', payload)).data;

export const updateJobPost = async ({ id, payload }) =>
  (await api.put(`/job-posts/${id}`, payload)).data;

export const importJobPosts = async (file) => {
    const formData = new FormData();
    formData.append('file', file);
    return (await api.post('/job-posts/import', formData)).data;
};

export const downloadJobPostsImportTemplate = async () =>
    (await api.get('/job-posts/import/template', { responseType: 'blob' })).data;

export const getJobPostAssignments = async (params) =>
  (await api.get('/job-posts/assignments', { params })).data;

export const getJobPostAssignmentPosts = async () =>
  (await api.get('/job-posts/assignments/job-posts')).data;

export const assignJobPost = async (payload) =>
  (await api.post('/job-posts/assignments', payload)).data;

export const endJobPostAssignment = async ({ id, endDate }) =>
  (await api.post(`/job-posts/assignments/${id}/end`, { endDate })).data;

export const importJobPostAssignments = async (file) => {
  const formData = new FormData();
  formData.append('file', file);
  return (await api.post('/job-posts/assignments/import', formData)).data;
};

export const downloadJobPostAssignmentsImportTemplate = async () =>
  (await api.get('/job-posts/assignments/import/template', { responseType: 'blob' })).data;
