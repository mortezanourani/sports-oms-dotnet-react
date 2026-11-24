import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as api from '../api/jobPostTypes';

export function useJobPostTypes() {
    return useQuery({ queryKey: ['jobPostTypes'], queryFn: api.getJobPostTypes });
}

export function useCreateJobPostType() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.createJobPostType,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['jobPostTypes'] }),
    });
}

export function useUpdateJobPostType() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.updateJobPostType,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['jobPostTypes'] }),
    });
}

export function useImportJobPostTypes() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.importJobPostTypes,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['jobPostTypes'] }),
    });
}
