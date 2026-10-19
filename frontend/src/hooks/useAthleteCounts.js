import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import * as api from '../api/athleteCounts';

export function useAthleteCounts(params) {
  return useQuery({ queryKey: ['athleteCounts', params], queryFn: () => api.getAthleteCounts(params) });
}

export function useAthleteCountSummary(params) {
  return useQuery({
    queryKey: ['athleteCountSummary', params],
    queryFn: () => api.getAthleteCountSummary(params),
  });
}

export function useAthleteCountSummaryFilters(params) {
  return useQuery({
    queryKey: ['athleteCountSummaryFilters', params],
    queryFn: () => api.getAthleteCountSummaryFilters(params),
  });
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
    onSuccess: () => Promise.all([
      qc.invalidateQueries({ queryKey: ['athleteCounts'] }),
      qc.invalidateQueries({ queryKey: ['athleteCountSummary'] }),
      qc.invalidateQueries({ queryKey: ['athleteCountSummaryFilters'] }),
    ]),
  });
}

export function useCreateAthleteCount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.createAthleteCount,
    onSuccess: () => Promise.all([
      qc.invalidateQueries({ queryKey: ['athleteCounts'] }),
      qc.invalidateQueries({ queryKey: ['athleteCountSummary'] }),
      qc.invalidateQueries({ queryKey: ['athleteCountSummaryFilters'] }),
    ]),
  });
}

export function useUpdateAthleteCount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.updateAthleteCount,
    onSuccess: () => Promise.all([
      qc.invalidateQueries({ queryKey: ['athleteCounts'] }),
      qc.invalidateQueries({ queryKey: ['athleteCountSummary'] }),
      qc.invalidateQueries({ queryKey: ['athleteCountSummaryFilters'] }),
    ]),
  });
}

export function useDeleteAthleteCount() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.deleteAthleteCount,
    onSuccess: () => Promise.all([
      qc.invalidateQueries({ queryKey: ['athleteCounts'] }),
      qc.invalidateQueries({ queryKey: ['athleteCountSummary'] }),
      qc.invalidateQueries({ queryKey: ['athleteCountSummaryFilters'] }),
    ]),
  });
}
