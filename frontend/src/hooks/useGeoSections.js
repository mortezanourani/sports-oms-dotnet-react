import { useState } from 'react';
import { useAuth } from './useAuth';
import { useProvinces, useCities } from './useLocations';

export function useGeoSections() {
    const auth = useAuth();
    const { data: allProvinces } = useProvinces();
    const [masteryProvinceId, setMasteryProvinceId] = useState(null);
    const [unscopedCityId, setUnscopedCityId] = useState(null);
    const selectedProvinceId = auth.scopeLevel === 'Mastery'
        ? masteryProvinceId
        : auth.scopeProvinceId;
    const selectedCityId = auth.scopeLevel === 'City'
        ? auth.scopeCityId
        : unscopedCityId;
    const { data: allCities } = useCities(selectedProvinceId);

    // Mastery section only exists for Mastery-scope users. Province section
    // exists for Mastery (free choice) and Province (locked to their own).
    // City section always exists, in every scope, just with progressively
    // more restricted — or entirely absent — selectors.
    const showMastery = auth.scopeLevel === 'Mastery';
    const showProvinceSection = auth.scopeLevel === 'Mastery' || auth.scopeLevel === 'Province';

    const provinceOptions = auth.scopeLevel === 'Mastery'
        ? allProvinces
        : allProvinces?.filter((p) => p.id === auth.scopeProvinceId);

    const cityOptions = auth.scopeLevel === 'City'
        ? allCities?.filter((c) => c.id === auth.scopeCityId)
        : allCities;

    return {
        auth,
        showMastery,
        showProvinceSection,
        // Province selector is only interactive for Mastery-scope users —
        // Province/City-scoped users see it locked (or hidden) to their own region.
        provinceLocked: auth.scopeLevel !== 'Mastery',
        cityLocked: auth.scopeLevel === 'City',
        selectedProvinceId,
        setSelectedProvinceId: setMasteryProvinceId,
        provinceOptions,
        selectedCityId,
        setSelectedCityId: setUnscopedCityId,
        cityOptions,
    };
}
