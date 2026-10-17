import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as api from '../api/profile';

export function useProfile() {
    return useQuery({
        queryKey: ['profile'],
        queryFn: api.getProfile,
    });
}

export function useUpdateProfile() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: api.updateProfile,
        onSuccess: () => queryClient.invalidateQueries({ queryKey: ['profile'] }),
    });
}

export function useChangePassword() {
    return useMutation({ mutationFn: api.changePassword });
}
