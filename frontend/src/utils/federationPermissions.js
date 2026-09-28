const ALLOWED_ROLES = ['Admin', 'Manager', 'SportsManager', 'SportsOfficer'];

export function canManageMastery({ role, scopeLevel }) {
    return ALLOWED_ROLES.includes(role) && scopeLevel === 'Mastery';
}

export function canManageProvincial({ role, scopeLevel, scopeProvinceId }, targetProvinceId) {
    if (!ALLOWED_ROLES.includes(role)) return false;
    if (scopeLevel === 'Mastery') return true;
    if (scopeLevel === 'Province') return scopeProvinceId === targetProvinceId;
    return false;
}

export function canManageCity({ role, scopeLevel, scopeProvinceId, scopeCityId }, targetCityId, targetProvinceId) {
    if (!ALLOWED_ROLES.includes(role)) return false;
    if (scopeLevel === 'Mastery') return true;
    if (scopeLevel === 'Province') return scopeProvinceId === targetProvinceId;
    if (scopeLevel === 'City') return scopeCityId === targetCityId;
    return false;
}
