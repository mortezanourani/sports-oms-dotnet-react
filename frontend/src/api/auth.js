import api from './axios';

export async function login(username, password) {
    const { data } = await api.post('/auth/login', { username, password });
    localStorage.setItem('access_token', data.access);
    localStorage.setItem('refresh_token', data.refresh);
    return data;
}

export function logout() {
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
}
