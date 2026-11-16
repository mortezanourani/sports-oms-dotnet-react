import api from './axios';

export const getJobPosts = async () => (await api.get('/job-posts')).data;

export const createJobPost = async (payload) =>
  (await api.post('/job-posts', payload)).data;

export const updateJobPost = async ({ id, payload }) =>
  (await api.put(`/job-posts/${id}`, payload)).data;

export const getJobPostAssignments = async (params) =>
  (await api.get('/job-posts/assignments', { params })).data;

export const assignJobPost = async (payload) =>
  (await api.post('/job-posts/assignments', payload)).data;

export const endJobPostAssignment = async ({ id, endedOn }) =>
  (await api.post(`/job-posts/assignments/${id}/end`, { endedOn })).data;
