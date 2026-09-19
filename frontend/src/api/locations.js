import api from './axios';

export const getProvinces = async () => {
    const { data } = await api.get('/locations/provinces');
    return data;
};

export const getCities = async (provinceId) => {
    const { data } = await api.get('/locations/cities', {
        params: provinceId ? { provinceId } : {},
    });
    return data;
};
