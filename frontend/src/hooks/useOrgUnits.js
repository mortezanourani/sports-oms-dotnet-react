import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as api from '../api/orgUnits';

export function useOrgUnits() {
    return useQuery({ queryKey: ['orgUnits'], queryFn: api.getOrgUnits });
}

export function useCreateOrgUnit() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.createOrgUnit,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['orgUnits'] }),
    });
}

export function useUpdateOrgUnit() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.updateOrgUnit,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['orgUnits'] }),
    });
}

export function useImportOrgUnits() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.importOrgUnits,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['orgUnits'] }),
    });
}
