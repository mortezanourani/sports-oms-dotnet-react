import { useState } from 'react';
import { Table, Button, Typography, Popconfirm, message, Space, Tag, Form, Modal, Input } from 'antd';
import { PlusOutlined, DeleteOutlined, HistoryOutlined, EditOutlined } from '@ant-design/icons';
import { useFederations, useDeleteFederation, useUpdateFederation } from '../../../hooks/useFederations';
import { useAuth } from '../../../hooks/useAuth';
import { canManageMastery } from '../../../utils/federationPermissions';
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
    const [editingFederation, setEditingFederation] = useState(null);
    const [editForm] = Form.useForm();
    const updateMutation = useUpdateFederation();

    const openEdit = (record) => {
        setEditingFederation(record);
        editForm.setFieldsValue({ name: record.displayName });
    };

    const handleEditFinish = async (values) => {
        try {
            await updateMutation.mutateAsync({ id: editingFederation.id, payload: values });
            message.success('با موفقیت ویرایش شد');
            setEditingFederation(null);
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطایی رخ داد');
        }
    };

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
                    {canManageMastery(auth) && (
                        <Button icon={<EditOutlined />} size="small" onClick={() => openEdit(record)} />
                    )}
                    <Button icon={<HistoryOutlined />} size="small" onClick={() => setPositionsFederation(record)}>سوابق</Button>
                    {canManageMastery(auth) && (
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
            {canManageMastery(auth) && (
                <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>افزودن فدراسیون کشوری</Button>
            )}

            <Table
                rowKey="id" columns={columns} dataSource={federations} loading={isLoading} pagination={false}
            />

            <Modal
                title="ویرایش فدراسیون کشوری" open={!!editingFederation} onCancel={() => setEditingFederation(null)}
                onOk={() => editForm.submit()} confirmLoading={updateMutation.isPending}
                okText="ذخیره" cancelText="انصراف" destroyOnClose
            >
                <Form form={editForm} layout="vertical" onFinish={handleEditFinish}>
                    <Form.Item name="name" label="نام فدراسیون" rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                </Form>
            </Modal>
            <FederationFormModal level="National" open={createOpen} onClose={() => setCreateOpen(false)} />
            <PositionsModal federation={positionsFederation} open={!!positionsFederation} onClose={() => setPositionsFederation(null)} />
        </div>
    );
}
