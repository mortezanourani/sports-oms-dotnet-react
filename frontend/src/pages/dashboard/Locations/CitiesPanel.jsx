import { useState } from 'react';
import { Table, Button, Space, Popconfirm, message, Modal, Form, Input } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { useCities, useCreateCity, useUpdateCity, useDeleteCity } from '../../../hooks/useLocations';

export default function CitiesPanel({ province }) {
    const { data: cities, isLoading } = useCities(province.id);
    const [modalOpen, setModalOpen] = useState(false);
    const [editingCity, setEditingCity] = useState(null);
    const [form] = Form.useForm();

    const createMutation = useCreateCity();
    const updateMutation = useUpdateCity();
    const deleteMutation = useDeleteCity();

    const openCreate = () => {
        setEditingCity(null);
        form.resetFields();
        setModalOpen(true);
    };

    const openEdit = (city) => {
        setEditingCity(city);
        form.setFieldsValue({ name: city.name });
        setModalOpen(true);
    };

    const handleFinish = async (values) => {
        // provinceId is always the CURRENT expanded province, not something
        // the user picks in this small form — a city being managed from
        // inside "Tehran"'s expanded panel is always being added to Tehran.
        // Moving a city to a different province is an edge case better
        // handled, if ever needed, from a dedicated city-management view,
        // not this quick inline panel.
        const payload = { provinceId: province.id, name: values.name };
        try {
            if (editingCity) {
                await updateMutation.mutateAsync({ id: editingCity.id, payload });
            } else {
                await createMutation.mutateAsync(payload);
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
            message.error(err.response?.data?.detail || 'خطا در حذف');
        }
    };

    const columns = [
        { title: 'نام شهر', dataIndex: 'name', key: 'name' },
        {
            title: 'عملیات', key: 'actions',
            render: (_, record) => (
                <Space>
                    <Button icon={<EditOutlined />} size="small" onClick={() => openEdit(record)} />
                    <Popconfirm title="حذف این شهر؟" onConfirm={() => handleDelete(record.id)} okText="بله" cancelText="خیر">
                        <Button icon={<DeleteOutlined />} size="small" danger />
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    return (
        <div style={{ padding: '8px 32px' }}>
            <Button icon={<PlusOutlined />} size="small" onClick={openCreate} style={{ marginBottom: 12 }}>
                افزودن شهر
            </Button>
            <Table
                rowKey="id"
                size="small"
                columns={columns}
                dataSource={cities}
                loading={isLoading}
                pagination={false}
            />

            <Modal
                title={editingCity ? 'ویرایش شهر' : 'افزودن شهر جدید'}
                open={modalOpen}
                onCancel={() => setModalOpen(false)}
                onOk={() => form.submit()}
                confirmLoading={createMutation.isPending || updateMutation.isPending}
                okText="ذخیره"
                cancelText="انصراف"
                destroyOnClose
            >
                <Form form={form} layout="vertical" onFinish={handleFinish}>
                    <Form.Item name="name" label="نام شهر" rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                </Form>
            </Modal>
        </div>
    );
}
