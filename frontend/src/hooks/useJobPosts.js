import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as api from '../api/jobPosts';

export function useJobPosts() {
  return useQuery({ queryKey: ['jobPosts'], queryFn: api.getJobPosts });
}

export function useJobPostAssignments(params) {
  return useQuery({
    queryKey: ['jobPosts', 'assignments', params],
    queryFn: () => api.getJobPostAssignments(params),
  });
}

export function useCreateJobPost() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: api.createJobPost,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['jobPosts'] }),
  });
}

export function useUpdateJobPost() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: api.updateJobPost,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['jobPosts'] }),
  });
}

export function useImportJobPosts() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: api.importJobPosts,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['jobPosts'] }),
  });
}

export function useAssignJobPost() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: api.assignJobPost,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['jobPosts', 'assignments'] }),
  });
}

export function useEndJobPostAssignment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: api.endJobPostAssignment,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['jobPosts', 'assignments'] }),
  });
}
