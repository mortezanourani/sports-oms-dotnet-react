import { useState } from 'react';
import { Table, Button, Popconfirm, message, Space } from 'antd';
import { PlusOutlined, DeleteOutlined, HistoryOutlined } from '@ant-design/icons';
import { useFederations, useDeleteFederation } from '../../../hooks/useFederations';
import { useAuth } from '../../../hooks/useAuth';
import { canManageCity } from '../../../utils/federationPermissions';
import FederationFormModal from './FederationFormModal';
import PositionsModal from './PositionsModal';

export default function CityFederationsPanel({ provincialFederation }) {
    const auth = useAuth();
    const { data: cityFederations, isLoading } = useFederations({ parentFederationId: provincialFederation.id });
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

    // Whether THIS user can create a city-level federation somewhere under
    // this specific provincial federation at all — used to decide whether
    // to show the "add" button. The actual target city is chosen inside the
    // modal; this is just "is there any point showing the button."
    const canCreateHere =
        auth.scopeLevel === 'National' ||
        (auth.scopeLevel === 'Province' && auth.scopeProvinceId === provincialFederation.provinceId) ||
        auth.scopeLevel === 'City';

    const columns = [
        { title: 'نام', dataIndex: 'displayName' },
        {
            title: 'عملیات', key: 'actions',
            render: (_, record) => (
                <Space>
                    <Button icon={<HistoryOutlined />} size="small" onClick={() => setPositionsFederation(record)}>سوابق</Button>
                    {canManageCity(auth, record.cityId, provincialFederation.provinceId) && (
                        <Popconfirm title="حذف این فدراسیون؟" onConfirm={() => handleDelete(record.id)} okText="بله" cancelText="خیر">
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
                    افزودن فدراسیون شهری
                </Button>
            )}
            <Table
                rowKey="id" size="small" columns={columns}
                dataSource={cityFederations} loading={isLoading} pagination={false}
                locale={{ emptyText: 'فدراسیون شهری ثبت نشده است' }}
            />
            <FederationFormModal level="City" parentFederation={provincialFederation} open={createOpen} onClose={() => setCreateOpen(false)} />
            <PositionsModal federation={positionsFederation} open={!!positionsFederation} onClose={() => setPositionsFederation(null)} />
        </div>
    );
}
