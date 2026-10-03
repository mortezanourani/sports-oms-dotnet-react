import { useState } from 'react';
import { Tabs, Table, Button, Typography, Badge } from 'antd';
import { PlusOutlined, PaperClipOutlined } from '@ant-design/icons';
import { useInbox, useSent } from '../../../hooks/useMessages';
import ComposeModal from './ComposeModal';
import MessageDetail from './MessageDetail';

const { Title } = Typography;

export default function MessagesPage() {
    const [activeTab, setActiveTab] = useState('inbox');
    const [page, setPage] = useState(1);
    const [composeOpen, setComposeOpen] = useState(false);
    const [selectedMessageId, setSelectedMessageId] = useState(null);

    const { data: inboxData, isLoading: inboxLoading } = useInbox({ page, pageSize: 20 });
    const { data: sentData, isLoading: sentLoading } = useSent({ page, pageSize: 20 });

    const data = activeTab === 'inbox' ? inboxData : sentData;
    const isLoading = activeTab === 'inbox' ? inboxLoading : sentLoading;

    const columns = [
        ...(activeTab === 'inbox' ? [{ title: 'فرستنده', dataIndex: 'senderName' }] : []),
        {
            title: 'موضوع', dataIndex: 'subject',
            render: (subject, record) => (
                // Unread messages render bold — a lightweight, purely visual cue
                // reflecting record.isRead, which the backend's MessageListOutDto
                // already computes correctly per-viewer (always true in the Sent
                // tab, real read-state in Inbox).
                <span style={{ fontWeight: record.isRead ? 'normal' : 'bold' }}>
          {subject || '(بدون موضوع)'}
        </span>
            ),
        },
        { title: 'پیش‌نمایش', dataIndex: 'bodyPreview' },
        {
            title: '', dataIndex: 'attachmentCount',
            render: (count) => count > 0 && <PaperClipOutlined title={`${count} پیوست`} />,
            width: 40,
        },
        { title: 'تاریخ', dataIndex: 'createdAt', render: (v) => new Date(v).toLocaleDateString('fa-IR') },
    ];

    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
                <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>پیام‌ها</Title>
                <Button type="primary" icon={<PlusOutlined />} onClick={() => setComposeOpen(true)}>پیام جدید</Button>
            </div>

            <Tabs
                activeKey={activeTab}
                onChange={(key) => { setActiveTab(key); setPage(1); }}
                items={[
                    { key: 'inbox', label: <Badge dot={inboxData?.items?.some((m) => !m.isRead)}>صندوق دریافت</Badge> },
                    { key: 'sent', label: 'ارسال‌شده‌ها' },
                ]}
            />

            <Table
                rowKey="id"
                columns={columns}
                dataSource={data?.items}
                loading={isLoading}
                pagination={{ current: page, pageSize: 20, total: data?.count, onChange: setPage }}
                onRow={(record) => ({
                    onClick: () => setSelectedMessageId(record.id),
                    style: { cursor: 'pointer' },
                })}
            />

            <ComposeModal open={composeOpen} onClose={() => setComposeOpen(false)} />
            {selectedMessageId && (
                <MessageDetail
                    messageId={selectedMessageId}
                    open={!!selectedMessageId}
                    onClose={() => setSelectedMessageId(null)}
                    isSentView={activeTab === 'sent'}
                />
            )}
        </div>
    );
}
