import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import * as api from '../api/locations';

export function useProvinces() {
    return useQuery({ queryKey: ['provinces'], queryFn: api.getProvinces });
}

export function useImportLocations() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.importLocations,
        onSuccess: () => {
            qc.invalidateQueries({ queryKey: ['provinces'] });
            qc.invalidateQueries({ queryKey: ['cities'] });
        },
    });
}

export function useCreateProvince() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.createProvince,
        onSuccess: () => qc.invalidateQueries({ queryKey: ['provinces'] }),
    });
}

export function useUpdateProvince() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.updateProvince,
        onSuccess: () => qc.invalidateQueries({ queryKey: ['provinces'] }),
    });
}

export function useDeleteProvince() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.deleteProvince,
        onSuccess: () => qc.invalidateQueries({ queryKey: ['provinces'] }),
    });
}

export function useCities(provinceId) {
    return useQuery({
        queryKey: ['cities', provinceId],
        queryFn: () => api.getCities(provinceId),
        enabled: !!provinceId, // don't fetch cities until a province is actually selected
    });
}

export function useCreateCity() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.createCity,
        // Invalidates every 'cities' query regardless of which provinceId
        // it was scoped to — necessary because creating a city could
        // theoretically be assigned to any province via the form, not
        // necessarily the one currently expanded in the table.
        onSuccess: () => qc.invalidateQueries({ queryKey: ['cities'] }),
    });
}

export function useUpdateCity() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.updateCity,
        onSuccess: () => qc.invalidateQueries({ queryKey: ['cities'] }),
    });
}

export function useDeleteCity() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.deleteCity,
        onSuccess: () => qc.invalidateQueries({ queryKey: ['cities'] }),
    });
}