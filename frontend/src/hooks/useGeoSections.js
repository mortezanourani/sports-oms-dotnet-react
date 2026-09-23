import { useState, useEffect } from 'react';
import { useAuth } from './useAuth';
import { useProvinces, useCities } from './useLocations';

export function useGeoSections() {
    const auth = useAuth();
    const { data: allProvinces } = useProvinces();
    const [selectedProvinceId, setSelectedProvinceId] = useState(null);
    const [selectedCityId, setSelectedCityId] = useState(null);
    const { data: allCities } = useCities(selectedProvinceId);

    // Mastery section only exists for National-scope users. Province section
    // exists for National (free choice) and Province (locked to their own).
    // City section always exists, in every scope, just with progressively
    // more restricted — or entirely absent — selectors.
    const showMastery = auth.scopeLevel === 'National';
    const showProvinceSection = auth.scopeLevel === 'National' || auth.scopeLevel === 'Province';

    // Auto-lock the selection to the user's own region the moment we know
    // their scope — this is what makes a Province-scoped user's "Province
    // section" show their own data immediately, with no manual selection
    // step, and feeds the correct city list into their City section too.
    useEffect(() => {
        if (auth.scopeLevel === 'Province') setSelectedProvinceId(auth.scopeProvinceId);
        if (auth.scopeLevel === 'City') setSelectedCityId(auth.scopeCityId);
    }, [auth.scopeLevel, auth.scopeProvinceId, auth.scopeCityId]);

    const provinceOptions = auth.scopeLevel === 'National'
        ? allProvinces
        : allProvinces?.filter((p) => p.id === auth.scopeProvinceId);

    const cityOptions = auth.scopeLevel === 'City'
        ? allCities?.filter((c) => c.id === auth.scopeCityId)
        : allCities;

    return {
        auth,
        showMastery,
        showProvinceSection,
        // Province selector is only interactive for National-scope users —
        // Province/City-scoped users see it locked (or hidden) to their own region.
        provinceLocked: auth.scopeLevel !== 'National',
        cityLocked: auth.scopeLevel === 'City',
        selectedProvinceId, setSelectedProvinceId, provinceOptions,
        selectedCityId, setSelectedCityId, cityOptions,
    };
}
