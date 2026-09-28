import { useState } from 'react';
import { Table, Button, Popconfirm, message, Space } from 'antd';
import { PlusOutlined, DeleteOutlined, HistoryOutlined } from '@ant-design/icons';
import { useFederations, useDeleteFederation } from '../../../hooks/useFederations';
import { useAuth } from '../../../hooks/useAuth';
import { canManageProvincial } from '../../../utils/federationPermissions';
import FederationFormModal from './FederationFormModal';
import PositionsModal from './PositionsModal';
import CityFederationsPanel from './CityFederationsPanel';

export default function ProvincialFederationsPanel({ nationalFederation }) {
    const auth = useAuth();
    const { data: provincialFederations, isLoading } = useFederations({ parentFederationId: nationalFederation.id });
    const deleteMutation = useDeleteFederation();
    const [createOpen, setCreateOpen] = useState(false);
    const [positionsFederation, setPositionsFederation] = useState(null);

    const handleDelete = async (id) => {
        try {
            await deleteMutation.mutateAsync(id);
            message.success('حذف شد');
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطا در حذف');
        }
    };

    // A Mastery or Province-scoped user can always find SOME province to
    // create under (Mastery sees everything; Province-scoped always has
    // exactly their own) — only a City-scoped user can never create at this
    // level at all, matching the backend's CanManageProvincial, which
    // returns false unconditionally for City scope.
    const canCreateHere = auth.scopeLevel === 'Mastery' || auth.scopeLevel === 'Province';

    const columns = [
        { title: 'نام', dataIndex: 'displayName' },
        {
            title: 'عملیات', key: 'actions',
            render: (_, record) => (
                <Space>
                    <Button icon={<HistoryOutlined />} size="small" onClick={() => setPositionsFederation(record)}>سوابق</Button>
                    {canManageProvincial(auth, record.provinceId) && (
                        <Popconfirm title="حذف این فدراسیون؟ (شامل زیرمجموعه‌های شهری آن)" onConfirm={() => handleDelete(record.id)} okText="بله" cancelText="خیر">
                            <Button icon={<DeleteOutlined />} size="small" danger />
                        </Popconfirm>
                    )}
                </Space>
            ),
        },
    ];

    return (
        <div style={{ padding: '8px 32px' }}>
            {canCreateHere && (
                <Button icon={<PlusOutlined />} size="small" onClick={() => setCreateOpen(true)} style={{ marginBottom: 12 }}>
                    افزودن فدراسیون استانی
                </Button>
            )}
            <Table
                rowKey="id" size="small" columns={columns}
                dataSource={provincialFederations} loading={isLoading} pagination={false}
                locale={{ emptyText: 'فدراسیون استانی ثبت نشده است' }}
                expandable={{ expandedRowRender: (record) => <CityFederationsPanel provincialFederation={record} /> }}
            />
            <FederationFormModal level="Provincial" parentFederation={nationalFederation} open={createOpen} onClose={() => setCreateOpen(false)} />
            <PositionsModal federation={positionsFederation} open={!!positionsFederation} onClose={() => setPositionsFederation(null)} />
        </div>
    );
}
