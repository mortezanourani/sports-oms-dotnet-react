import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import * as api from '../api/users';

export function useUsers(params) {
  return useQuery({
    queryKey: ['users', params],
    queryFn: () => api.getUsers(params)
  });
}

export function useUser(id) {
  return useQuery({
    queryKey: ['users', id],
    queryFn: () => api.getUser(id),
    enabled: !!id,
  });
}

export function useCreateUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.createUser,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['users'] }),
  });
}

export function useUpdateUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.updateUser,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['users'] }),
  });
}

export function useDeactivateUser() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.deactivateUser,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['users'] }),
  });
}

export function useAssignFederationPosition() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: api.assignFederationPosition,
    onSuccess: (_data, variables) => {
      qc.invalidateQueries({ queryKey: ['userFederationPositions', variables.userId] });
      // Also invalidate the Federations page's position-history cache,
      // since assigning someone here changes what that page's
      // PositionsModal shows for the affected federation too — the two
      // pages share the same underlying data from different angles.
      qc.invalidateQueries({ queryKey: ['federationPositions'] });
    },
  });
}

export function useUserFederationPositions(userId) {
  return useQuery({
    queryKey: ['userFederationPositions', userId],
    queryFn: () => api.getUserFederationPositions(userId),
    enabled: !!userId,
  });
}
