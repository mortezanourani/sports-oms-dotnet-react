import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import * as api from '../api/messages';

export function useInbox(params) {
    return useQuery({ queryKey: ['messages', 'inbox', params], queryFn: () => api.getInbox(params) });
}

export function useSent(params) {
    return useQuery({ queryKey: ['messages', 'sent', params], queryFn: () => api.getSent(params) });
}

export function useMessageDetail(id) {
    return useQuery({
        queryKey: ['messages', 'detail', id],
        queryFn: () => api.getMessage(id),
        enabled: !!id,
    });
}

export function useUnreadCount() {
    return useQuery({
        queryKey: ['messages', 'unreadCount'],
        queryFn: api.getUnreadCount,
        // Polling every 30s is a reasonable, simple way to keep the sidebar
        // badge roughly current without building a real-time push mechanism
        // (WebSockets/SignalR) — appropriate for an internal reporting tool
        // where a message doesn't need to be noticed within seconds.
        refetchInterval: 30000,
    });
}

export function useRecipientOptions(params, enabled = true) {
    return useQuery({
        queryKey: ['messages', 'recipients', params],
        queryFn: () => api.getRecipientOptions(params),
        enabled,
    });
}

export function useSendMessage() {
    const qc = useQueryClient();
    return useMutation({
        mutationFn: api.sendMessage,
        onSuccess: () => qc.invalidateQueries({ queryKey: ['messages', 'sent'] }),
    });
}

export function useAddAttachment() {
    return useMutation({ mutationFn: api.addAttachment });
    // Deliberately no cache invalidation here — attachments are added
    // immediately after send, before the compose modal even closes, so
    // there's no stale UI state elsewhere that needs refreshing yet.
}

// Marking a message as read (which happens as a side effect of the
// backend's GET /messages/{id}) needs to update THREE separate pieces of
// UI that all depend on read-state: the inbox list's bold/unread styling,
// the sidebar badge count, and the currently-open detail view itself.
// This hook centralizes that invalidation so opening any message
// consistently refreshes all three, rather than each caller remembering
// to do it themselves.
export function useMarkAsReadEffect(queryClient, messageId) {
    return () => {
        queryClient.invalidateQueries({ queryKey: ['messages', 'inbox'] });
        queryClient.invalidateQueries({ queryKey: ['messages', 'unreadCount'] });
        queryClient.invalidateQueries({ queryKey: ['messages', 'detail', messageId] });
    };
}
