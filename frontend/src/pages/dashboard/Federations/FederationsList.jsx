import { useState } from 'react';
import { Table, Button, Typography, Popconfirm, message, Space, Tag } from 'antd';
import { PlusOutlined, DeleteOutlined, HistoryOutlined } from '@ant-design/icons';
import { useFederations, useDeleteFederation } from '../../../hooks/useFederations';
import { useAuth } from '../../../hooks/useAuth';
import { canManageNational } from '../../../utils/federationPermissions';
import { FEDERATION_CATEGORY_LABELS } from '../../../constants/labels';
import FederationFormModal from './FederationFormModal';
import PositionsModal from './PositionsModal';
import ProvincialFederationsPanel from './ProvincialFederationsPanel';

const { Title } = Typography;

export default function FederationsList() {
  const auth = useAuth();
  const { data: federations, isLoading } = useFederations({ level: 'National' });
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

  const columns = [
    { title: 'نام', dataIndex: 'displayName' },
    { title: 'دسته‌بندی', dataIndex: 'category', render: (v) => <Tag>{FEDERATION_CATEGORY_LABELS[v] || v}</Tag> },
    {
      title: 'عملیات', key: 'actions',
      render: (_, record) => (
          <Space>
            <Button icon={<HistoryOutlined />} size="small" onClick={() => setPositionsFederation(record)}>سوابق</Button>
            {canManageNational(auth) && (
                <Popconfirm title="حذف این فدراسیون؟ (شامل تمام زیرمجموعه‌های استانی و شهری آن)" onConfirm={() => handleDelete(record.id)} okText="بله" cancelText="خیر">
                  <Button icon={<DeleteOutlined />} size="small" danger />
                </Popconfirm>
            )}
          </Space>
      ),
    },
  ];

  return (
      <div>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
          <Title level={4}>فدراسیون‌ها</Title>
          {canManageNational(auth) && (
              <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>افزودن فدراسیون کشوری</Button>
          )}
        </div>

        <Table
            rowKey="id" columns={columns} dataSource={federations} loading={isLoading} pagination={false}
            expandable={{ expandedRowRender: (record) => <ProvincialFederationsPanel nationalFederation={record} /> }}
        />

        <FederationFormModal level="National" open={createOpen} onClose={() => setCreateOpen(false)} />
        <PositionsModal federation={positionsFederation} open={!!positionsFederation} onClose={() => setPositionsFederation(null)} />
      </div>
  );
}
