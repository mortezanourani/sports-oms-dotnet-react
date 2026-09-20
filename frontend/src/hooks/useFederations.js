import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import * as api from '../api/federations';

export function useFederations() {
  return useQuery({ queryKey: ['federations'], queryFn: api.getFederations });
}

export function useCreateFederation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.createFederation,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['federations'] }),
  });
}

export function useUpdateFederation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.updateFederation,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['federations'] }),
  });
}

export function useDeleteFederation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.deleteFederation,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['federations'] }),
  });
}

export function useFederationPositions(id) {
  return useQuery({
    queryKey: ['federationPositions', id],
    queryFn: () => api.getFederationPositions(id),
    enabled: !!id,
  });
}
