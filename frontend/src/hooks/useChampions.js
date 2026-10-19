import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import * as api from '../api/champions';

export function useChampions(params) {
    return useQuery({ queryKey: ['champions', params], queryFn: () => api.getChampions(params) });
}

export function useChampionFederations() {
    return useQuery({
        queryKey: ['champions', 'federations'],
        queryFn: api.getChampionFederations,
    });
}

export function useChampion(id) {
    return useQuery({
        queryKey: ['champions', id],
        queryFn: () => api.getChampion(id),
        enabled: !!id,
    });
}

export function useCreateChampion() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.createChampion,
        onSuccess: () => qc.invalidateQueries({ queryKey: ['champions'] }),
    });
}

export function useUpdateChampion() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.updateChampion,
        onSuccess: () => qc.invalidateQueries({ queryKey: ['champions'] }),
    });
}

export function useDeleteChampion() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.deleteChampion,
        onSuccess: () => qc.invalidateQueries({ queryKey: ['champions'] }),
    });
}

export function useAddMedal() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.addMedal,
        // Invalidating the specific champion's detail query is what makes the
        // medals table inside the modal refresh immediately — invalidating
        // just ['champions'] (the list) wouldn't touch ['champions', id]
        // (the detail), since React Query treats these as separate cache
        // entries by default.
        onSuccess: (_data, variables) => {
            qc.invalidateQueries({ queryKey: ['champions', variables.championId] });
            qc.invalidateQueries({ queryKey: ['champions'] }); // list's medalCount also needs refreshing
        },
    });
}

export function useDeleteMedal() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.deleteMedal,
        onSuccess: (_data, variables) => {
            qc.invalidateQueries({ queryKey: ['champions', variables.championId] });
            qc.invalidateQueries({ queryKey: ['champions'] });
        },
    });
}

export function useImportChampions() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.importChampions,
        onSuccess: () => {
            qc.invalidateQueries({ queryKey: ['champions'] });
        },
    });
}
