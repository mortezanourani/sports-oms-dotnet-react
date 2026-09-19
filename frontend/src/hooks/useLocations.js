import { useQuery } from '@tanstack/react-query';
import * as api from '../api/locations';

export function useProvinces() {
    return useQuery({ queryKey: ['provinces'], queryFn: api.getProvinces });
}

export function useCities(provinceId) {
    return useQuery({
        queryKey: ['cities', provinceId],
        queryFn: () => api.getCities(provinceId),
        enabled: !!provinceId, // don't fetch cities until a province is actually selected
    });
}
