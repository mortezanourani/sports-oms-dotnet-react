import { useQuery } from '@tanstack/react-query';
import api from '../api/axios';

export function useFederations() {
    return useQuery({
        queryKey: ['federations'],
        queryFn: async () => (await api.get('/federations')).data,
    });
}
