import api from './axios';

export async function getProfile() {
    const { data } = await api.get('/profile');
    return data;
}

export async function updateProfile(profile) {
    const { data } = await api.put('/profile', profile);
    if (!data.accessToken) {
        throw new Error('The profile response did not include a replacement access token.');
    }

    localStorage.setItem('access_token', data.accessToken);
    window.dispatchEvent(new CustomEvent('profile-updated', {
        detail: {
            fullName: [data.firstName, data.lastName].filter(Boolean).join(' '),
        },
    }));

    return data;
}

export async function changePassword(passwords) {
    const { data } = await api.post('/profile/password', passwords);
    return data;
}
