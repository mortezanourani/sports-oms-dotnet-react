import { useQuery } from '@tanstack/react-query';
import * as api from '../api/public';

export function usePublicFacilityStats(params) {
    return useQuery({ queryKey: ['public', 'facilityStats', params], queryFn: () => api.getPublicFacilityStats(params) });
}

export function usePublicChampions(params) {
    return useQuery({ queryKey: ['public', 'champions', params], queryFn: () => api.getPublicChampions(params) });
}

export function usePublicAthleteStats(params) {
    return useQuery({ queryKey: ['public', 'athleteStats', params], queryFn: () => api.getPublicAthleteStats(params) });
}

export function usePublicFederations() {
    return useQuery({ queryKey: ['public', 'federations'], queryFn: api.getPublicFederations });
}

export function usePublicProvinces() {
    return useQuery({ queryKey: ['public', 'provinces'], queryFn: api.getPublicProvinces });
}
