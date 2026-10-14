import { useState } from 'react';
import { Table, Button, Popconfirm, message, Space, Tag, Typography } from 'antd';
import { DeleteOutlined, HistoryOutlined, PlusOutlined } from '@ant-design/icons';
import { useFederations, useDeleteFederation } from '../../../hooks/useFederations';
import { useAuth } from '../../../hooks/useAuth';
import { canManageProvincial, canManageCity } from '../../../utils/federationPermissions';
import { FEDERATION_CATEGORY_LABELS } from '../../../constants/labels';
import PositionsModal from './PositionsModal';
import FederationFormModal from './FederationFormModal';

const { Text } = Typography;

export default function FederationsFlatTable({ level, provinceId, cityId }) {
    const auth = useAuth();
    const { data: federations, isLoading } = useFederations({
        level,
        ...(level === 'Provincial' ? { provinceId } : { cityId }),
    });
    const deleteMutation = useDeleteFederation();
    const [positionsFederation, setPositionsFederation] = useState(null);
    const [createOpen, setCreateOpen] = useState(false);

    const canCreateHere =
        level === 'Provincial'
            ? !!provinceId && canManageProvincial(auth, provinceId)
            : !!cityId && canManageCity(auth, cityId, provinceId);

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
        { title: 'دسته‌بندی', dataIndex: 'category', render: (v) => <Tag>{v ? FEDERATION_CATEGORY_LABELS[v] || v : 'بدون دسته‌بندی'}</Tag> },
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
            <div style={{ marginBottom: 16 }}>
                {canCreateHere ? (
                    <Button icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
                        {level === 'Provincial' ? 'افزودن فدراسیون استانی' : 'افزودن فدراسیون شهری'}
                    </Button>
                ) : (
                    (provinceId || cityId) && (
                        <Text type="secondary">
                            {level === 'Provincial'
                                ? 'برای افزودن، ابتدا استان مورد نظر را انتخاب کنید یا سطح دسترسی شما اجازه ایجاد را نمی‌دهد.'
                                : 'برای افزودن، ابتدا شهر مورد نظر را انتخاب کنید یا سطح دسترسی شما اجازه ایجاد را نمی‌دهد.'}
                        </Text>
                    )
                )}
            </div>

            <Table
                rowKey="id" columns={columns} dataSource={federations} loading={isLoading} pagination={false}
                locale={{ emptyText: 'فدراسیونی یافت نشد' }}
            />

            <PositionsModal federation={positionsFederation} open={!!positionsFederation} onClose={() => setPositionsFederation(null)} />
            <FederationFormModal
                level={level}
                targetProvinceId={provinceId}
                targetCityId={cityId}
                open={createOpen}
                onClose={() => setCreateOpen(false)}
            />
        </div>
    );
}
