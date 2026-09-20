import { Drawer, Typography, List, Button, Tag, Space, Divider, message } from 'antd';
import { DownloadOutlined } from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { useMessageDetail, useMarkAsReadEffect } from '../../../hooks/useMessages';
import { downloadAttachment } from '../../../api/messages';
import { useEffect } from 'react';

const { Title, Paragraph, Text } = Typography;

export default function MessageDetail({ messageId, open, onClose, isSentView }) {
    const { data: msg, isLoading } = useMessageDetail(messageId);
    const queryClient = useQueryClient();
    const markReadEffect = useMarkAsReadEffect(queryClient, messageId);

    // Fires once after the detail actually loads — this is what refreshes
    // the inbox list's bold/unread state and the sidebar badge, reflecting
    // that opening this drawer already triggered the backend to mark the
    // message read (a side effect of GET /messages/{id} itself, not
    // something this component asks for separately).
    useEffect(() => {
        if (msg) markReadEffect();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [msg?.id]);

    const handleDownload = async (attachment) => {
        try {
            const blob = await downloadAttachment(attachment.id);
            const url = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = attachment.fileName;
            link.click();
            window.URL.revokeObjectURL(url);
        } catch (err) {
            message.error('خطا در دریافت فایل');
        }
    };

    return (
        <Drawer title="جزئیات پیام" open={open} onClose={onClose} width={480}>
            {isLoading || !msg ? (
                'در حال بارگذاری...'
            ) : (
                <>
                    <Title level={5}>{msg.subject || '(بدون موضوع)'}</Title>
                    <Text type="secondary">از طرف: {msg.senderName}</Text>
                    <Paragraph style={{ marginTop: 16, whiteSpace: 'pre-wrap' }}>{msg.body}</Paragraph>

                    {msg.attachments.length > 0 && (
                        <>
                            <Divider />
                            <Text strong>پیوست‌ها</Text>
                            <List
                                size="small"
                                dataSource={msg.attachments}
                                renderItem={(a) => (
                                    <List.Item
                                        actions={[
                                            <Button icon={<DownloadOutlined />} size="small" onClick={() => handleDownload(a)}>
                                                دانلود
                                            </Button>,
                                        ]}
                                    >
                                        {a.fileName}
                                    </List.Item>
                                )}
                            />
                        </>
                    )}

                    {isSentView && (
                        <>
                            <Divider />
                            <Text strong>وضعیت خواندن</Text>
                            <List
                                size="small"
                                dataSource={msg.recipients}
                                renderItem={(r) => (
                                    <List.Item>
                                        <Space>
                                            {r.userName}
                                            {r.isRead ? <Tag color="green">خوانده شده</Tag> : <Tag color="orange">خوانده نشده</Tag>}
                                        </Space>
                                    </List.Item>
                                )}
                            />
                        </>
                    )}
                </>
            )}
        </Drawer>
    );
}
