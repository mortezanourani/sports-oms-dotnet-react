const ALLOWED_ROLES = ['Admin', 'Manager', 'SportOfficer'];

export function canManageNational({ role, scopeLevel }) {
    return ALLOWED_ROLES.includes(role) && scopeLevel === 'National';
}

export function canManageProvincial({ role, scopeLevel, scopeProvinceId }, targetProvinceId) {
    if (!ALLOWED_ROLES.includes(role)) return false;
    if (scopeLevel === 'National') return true;
    if (scopeLevel === 'Province') return scopeProvinceId === targetProvinceId;
    return false;
}

export function canManageCity({ role, scopeLevel, scopeProvinceId, scopeCityId }, targetCityId, targetProvinceId) {
    if (!ALLOWED_ROLES.includes(role)) return false;
    if (scopeLevel === 'National') return true;
    if (scopeLevel === 'Province') return scopeProvinceId === targetProvinceId;
    if (scopeLevel === 'City') return scopeCityId === targetCityId;
    return false;
}
