import api from './axios';

export const getInbox = async (params) => (await api.get('/messages/inbox', { params })).data;
export const getSent = async (params) => (await api.get('/messages/sent', { params })).data;
export const getMessage = async (id) => (await api.get(`/messages/${id}`)).data;
export const getUnreadCount = async () => (await api.get('/messages/unread-count')).data;
export const getRecipientOptions = async () => (await api.get('/messages/recipients')).data;

export const sendMessage = async (payload) => (await api.post('/messages', payload)).data;

export const addAttachment = async ({ messageId, file }) => {
    const formData = new FormData();
    formData.append('file', file);
    const { data } = await api.post(`/messages/${messageId}/attachments`, formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
};

// Returns a raw Blob, not JSON — the download endpoint is a private,
// access-controlled file, so it must go through axios (which attaches the
// Authorization header via the interceptor already set up) rather than a
// plain <a href="..."> link, which the browser would request with no auth
// header at all and get correctly rejected by the backend.
export const downloadAttachment = async (attachmentId) => {
    const response = await api.get(`/messages/attachments/${attachmentId}/download`, {
        responseType: 'blob',
    });
    return response.data;
};
