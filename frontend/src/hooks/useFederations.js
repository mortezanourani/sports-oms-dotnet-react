import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import * as api from '../api/federations';

export function useFederations(params) {
  return useQuery({ queryKey: ['federations', params], queryFn: () => api.getFederations(params) });
}

export function useImportFederations() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.importFederations,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['federations'] });
      qc.invalidateQueries({ queryKey: ['federationPositions'] });
      qc.invalidateQueries({ queryKey: ['federationReps'] });
      qc.invalidateQueries({ queryKey: ['users'] });
    },
  });
}

export function useCreateFederation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.createFederation,
    // Invalidates every 'federations' query regardless of its params —
    // necessary because creating a Provincial federation changes both
    // its own parent's HasChildren flag AND the list of that parent's
    // children, two different cached queries at once.
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

export function useCreateFederationRep() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.createFederationRep,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['federationPositions'] });
      qc.invalidateQueries({ queryKey: ['federationReps'] });
    },
  });
}
