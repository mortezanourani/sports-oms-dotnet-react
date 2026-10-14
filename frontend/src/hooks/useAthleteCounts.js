import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import * as api from '../api/athleteCounts';

export function useAthleteCounts(params) {
  return useQuery({ queryKey: ['athleteCounts', params], queryFn: () => api.getAthleteCounts(params) });
}

export function useAthleteCount(id) {
  return useQuery({
    queryKey: ['athleteCounts', id],
    queryFn: () => api.getAthleteCount(id),
    enabled: !!id,
  });
}

export function useImportAthleteCounts() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.importAthleteCounts,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['athleteCounts'] }),
  });
}

export function useCreateAthleteCount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.createAthleteCount,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['athleteCounts'] }),
  });
}

export function useUpdateAthleteCount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.updateAthleteCount,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['athleteCounts'] }),
  });
}

export function useDeleteAthleteCount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.deleteAthleteCount,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['athleteCounts'] }),
  });
}
