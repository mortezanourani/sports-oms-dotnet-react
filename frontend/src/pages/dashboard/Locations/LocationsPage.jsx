import { useState } from 'react';
import { Table, Button, Space, Typography, Popconfirm, message, Modal, Form, Input } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { useProvinces, useCreateProvince, useUpdateProvince, useDeleteProvince } from '../../../hooks/useLocations';
import CitiesPanel from './CitiesPanel';

const { Title } = Typography;

export default function LocationsPage() {
    const { data: provinces, isLoading } = useProvinces();
    const [modalOpen, setModalOpen] = useState(false);
    const [editingProvince, setEditingProvince] = useState(null);
    const [form] = Form.useForm();

    const createMutation = useCreateProvince();
    const updateMutation = useUpdateProvince();
    const deleteMutation = useDeleteProvince();

    const openCreate = () => {
        setEditingProvince(null);
        form.resetFields();
        setModalOpen(true);
    };

    const openEdit = (province) => {
        setEditingProvince(province);
        form.setFieldsValue({ name: province.name });
        setModalOpen(true);
    };

    const handleFinish = async (values) => {
        try {
            if (editingProvince) {
                await updateMutation.mutateAsync({ id: editingProvince.id, payload: values });
            } else {
                await createMutation.mutateAsync(values);
            }
            message.success('با موفقیت ذخیره شد');
            setModalOpen(false);
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطایی رخ داد');
        }
    };

    const handleDelete = async (id) => {
        try {
            await deleteMutation.mutateAsync(id);
            message.success('حذف شد');
        } catch (err) {
            // This is exactly where the backend's "this province has cities
            // attached and can't be deleted" 409 message surfaces — no special
            // handling needed here beyond what's already generic across every
            // entity, since the backend already produces a clear Persian detail
            // message for this specific case.
            message.error(err.response?.data?.detail || 'خطا در حذف');
        }
    };

    const columns = [
        { title: 'نام استان', dataIndex: 'name', key: 'name' },
        {
            title: 'عملیات', key: 'actions',
            render: (_, record) => (
                <Space>
                    <Button icon={<EditOutlined />} size="small" onClick={() => openEdit(record)} />
                    <Popconfirm title="حذف این استان؟" onConfirm={() => handleDelete(record.id)} okText="بله" cancelText="خیر">
                        <Button icon={<DeleteOutlined />} size="small" danger />
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
                <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>استان‌ها و شهرها</Title>
                <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>افزودن استان جدید</Button>
            </div>

            <Table
                rowKey="id"
                columns={columns}
                dataSource={provinces}
                loading={isLoading}
                pagination={false}
                expandable={{
                    // Lazily rendering CitiesPanel only for the currently expanded
                    // row(s) — rather than fetching every province's cities up
                    // front — avoids firing 17+ simultaneous city-list requests
                    // the moment this page loads, most of which the user may never
                    // even look at.
                    expandedRowRender: (record) => <CitiesPanel province={record} />,
                }}
            />

            <Modal
                title={editingProvince ? 'ویرایش استان' : 'افزودن استان جدید'}
                open={modalOpen}
                onCancel={() => setModalOpen(false)}
                onOk={() => form.submit()}
                confirmLoading={createMutation.isPending || updateMutation.isPending}
                okText="ذخیره"
                cancelText="انصراف"
                destroyOnClose
            >
                <Form form={form} layout="vertical" onFinish={handleFinish}>
                    <Form.Item name="name" label="نام استان" rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                </Form>
            </Modal>
        </div>
    );
}
