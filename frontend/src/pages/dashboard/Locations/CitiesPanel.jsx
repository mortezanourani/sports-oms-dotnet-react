import { useState } from 'react';
import { Table, Button, Space, Popconfirm, message, Modal, Form, Input, Typography } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { useAllCities, useCities, useCreateCity, useUpdateCity, useDeleteCity } from '../../../hooks/useLocations';

export default function CitiesPanel({ provinceId, provinces }) {
    const { data: allCities, isLoading: isAllCitiesLoading } = useAllCities(!provinceId);
    const { data: provinceCities, isLoading: isProvinceCitiesLoading } = useCities(provinceId);
    const cities = provinceId ? provinceCities : allCities;
    const isLoading = provinceId ? isProvinceCitiesLoading : isAllCitiesLoading;
    const [modalOpen, setModalOpen] = useState(false);
    const [editingCity, setEditingCity] = useState(null);
    const [form] = Form.useForm();

    const createMutation = useCreateCity();
    const updateMutation = useUpdateCity();
    const deleteMutation = useDeleteCity();

    const openCreate = () => {
        if (!provinceId) return;
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
        const payload = { provinceId: editingCity?.provinceId || provinceId, name: values.name };
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
        ...(!provinceId ? [{ title: 'استان', dataIndex: 'provinceName', key: 'provinceName' }] : []),
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
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
                <Typography.Title level={5} style={{ margin: 0 }}>
                    {provinces?.find((province) => province.id === provinceId)?.name || 'همه استان‌ها'}
                </Typography.Title>
                <Button
                    type="primary"
                    icon={<PlusOutlined />}
                    onClick={openCreate}
                    disabled={!provinceId}
                >
                    افزودن شهر
                </Button>
            </div>
            <Table
                rowKey="id"
                columns={columns}
                dataSource={cities}
                loading={isLoading}
                pagination={{ pageSize: 10, showSizeChanger: false }}
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
