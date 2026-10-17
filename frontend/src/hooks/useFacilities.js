import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import * as api from '../api/facilities';

export function useFacilities(params) {
    return useQuery({
        queryKey: ['facilities', params],
        queryFn: () => api.getFacilities(params),
    });
}

export function useFacility(id) {
    return useQuery({
        queryKey: ['facilities', id],
        queryFn: () => api.getFacility(id),
        enabled: !!id,
    });
}

export function useCreateFacility() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.createFacility,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['facilities'] }),
    });
}

export function useUpdateFacility() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.updateFacility,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['facilities'] }),
    });
}

export function useDeleteFacility() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.deleteFacility,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['facilities'] }),
    });
}

export function useImportPrivateFacilities() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.importPrivateFacilities,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['facilities'] }),
    });
}

export function useImportMsyGovFacilities() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.importMsyGovFacilities,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['facilities'] }),
    });
}
