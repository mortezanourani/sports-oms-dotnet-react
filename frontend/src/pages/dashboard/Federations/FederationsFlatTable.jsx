import { useState } from 'react';
import { Table, Button, Popconfirm, message, Space, Tag } from 'antd';
import { DeleteOutlined, HistoryOutlined } from '@ant-design/icons';
import { useFederations, useDeleteFederation } from '../../../hooks/useFederations';
import { useAuth } from '../../../hooks/useAuth';
import { canManageProvincial, canManageCity } from '../../../utils/federationPermissions';
import { FEDERATION_CATEGORY_LABELS } from '../../../constants/labels';
import PositionsModal from './PositionsModal';

// level: 'Provincial' | 'City' — decides which permission-check function
// to use per row, and which filter param (provinceId vs cityId) to send.
export default function FederationsFlatTable({ level, provinceId, cityId }) {
    const auth = useAuth();
    const { data: federations, isLoading } = useFederations({
        level,
        ...(level === 'Provincial' ? { provinceId } : { cityId }),
    });
    const deleteMutation = useDeleteFederation();
    const [positionsFederation, setPositionsFederation] = useState(null);

    const handleDelete = async (id) => {
        try {
            await deleteMutation.mutateAsync(id);
            message.success('حذف شد');
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطا در حذف');
        }
    };

    const canManageRow = (record) =>
        level === 'Provincial'
            ? canManageProvincial(auth, record.provinceId)
            : canManageCity(auth, record.cityId, provinceId);

    const columns = [
        { title: 'نام', dataIndex: 'displayName' },
        { title: 'دسته‌بندی', dataIndex: 'category', render: (v) => <Tag>{FEDERATION_CATEGORY_LABELS[v] || v}</Tag> },
        { title: 'فدراسیون والد', dataIndex: 'parentFederationName' },
        {
            title: 'عملیات', key: 'actions',
            render: (_, record) => (
                <Space>
                    <Button icon={<HistoryOutlined />} size="small" onClick={() => setPositionsFederation(record)}>سوابق</Button>
                    {canManageRow(record) && (
                        <Popconfirm title="حذف این فدراسیون؟" onConfirm={() => handleDelete(record.id)} okText="بله" cancelText="خیر">
                            <Button icon={<DeleteOutlined />} size="small" danger />
                        </Popconfirm>
                    )}
                </Space>
            ),
        },
    ];

    return (
        <div>
            <Table
                rowKey="id" columns={columns} dataSource={federations} loading={isLoading} pagination={false}
                locale={{ emptyText: 'فدراسیونی یافت نشد' }}
            />
            <PositionsModal federation={positionsFederation} open={!!positionsFederation} onClose={() => setPositionsFederation(null)} />
        </div>
    );
}
