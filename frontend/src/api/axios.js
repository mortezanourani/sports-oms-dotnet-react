import axios from 'axios';

const api = axios.create({
    baseURL: import.meta.env.VITE_API_URL,
});

let refreshPromise = null;

api.interceptors.request.use((config) => {
    const token = localStorage.getItem('access_token');
    if (token) config.headers.Authorization = `Bearer ${token}`;
    return config;
});

api.interceptors.response.use(
    (response) => response,
    async (error) => {
        const original = error.config;
        if (error.response?.status === 401 && !original._retry) {
            original._retry = true;
            const refresh = localStorage.getItem('refresh_token');
            if (refresh) {
                try {
                    if (!refreshPromise) {
                        refreshPromise = axios.post(`${import.meta.env.VITE_API_URL}/auth/refresh`, {
                            refreshToken: refresh,
                        }).then(({ data }) => {
                            if (!data.access || !data.refresh) {
                                throw new Error('The refresh response did not include replacement tokens.');
                            }
                            localStorage.setItem('access_token', data.access);
                            localStorage.setItem('refresh_token', data.refresh);
                            return data;
                        }).finally(() => {
                            refreshPromise = null;
                        });
                    }
                    const { data } = await refreshPromise;
                    localStorage.setItem('access_token', data.access);
                    original.headers.Authorization = `Bearer ${data.access}`;
                    return api(original);
                } catch {
                    localStorage.removeItem('access_token');
                    localStorage.removeItem('refresh_token');
                    window.location.href = '/login';
                }
            }
        }
        return Promise.reject(error);
    }
);

export default api;
