import { jwtDecode } from 'jwt-decode';

export function useAuth() {
    const token = localStorage.getItem('access_token');

    const empty = {
        isAuthenticated: false,
        role: null,
        fullName: null,
        scopeLevel: null,
        scopeProvinceId: null,
        scopeCityId: null,
    };

    if (!token) return empty;

    try {
        const decoded = jwtDecode(token);
        const isExpired = decoded.exp * 1000 < Date.now();
        if (isExpired) return empty;

        return {
            isAuthenticated: true,
            role: decoded.role,
            fullName: decoded.full_name,
            scopeLevel: decoded.scope_level,
            scopeProvinceId: decoded.scope_province_id || null,
            scopeCityId: decoded.scope_city_id || null,
        };
    } catch {
        return empty;
    }
}
