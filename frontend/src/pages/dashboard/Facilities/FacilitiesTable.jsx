import { useEffect, useState } from 'react';
import { Table, Button, Space, Typography, Popconfirm, message, Select, Modal, Form, Input } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import {
    useFacilities, useFacility, useCreateFacility, useUpdateFacility, useDeleteFacility,
} from '../../../hooks/useFacilities';
import { useProvinces, useCities } from '../../../hooks/useLocations';

const { Title } = Typography;
const FACILITY_TYPE_LABELS = { Msy: 'MSY', Private: 'خصوصی', Gov: 'دولتی' };

export default function FacilitiesTable({ filters }) {
    const [page, setPage] = useState(1);
    const [facilityType, setFacilityType] = useState(undefined);
    const [modalOpen, setModalOpen] = useState(false);
    const [editingId, setEditingId] = useState(null);
    const [selectedProvinceId, setSelectedProvinceId] = useState(null);
    const [form] = Form.useForm();

    const { data, isLoading } = useFacilities({ page, pageSize: 20, facilityType, ...filters });
    const { data: editingFacility, isFetching: isFetchingEditingFacility } = useFacility(editingId);
    const { data: provinces } = useProvinces();
    const { data: cities } = useCities(selectedProvinceId);

    const createMutation = useCreateFacility();
    const updateMutation = useUpdateFacility();
    const deleteMutation = useDeleteFacility();

    // Populates the form once the full facility record has loaded — only
    // relevant in edit mode, since create mode has nothing to pre-fill.
    useEffect(() => {
        if (editingId && editingFacility) {
            form.setFieldsValue(editingFacility);
            setSelectedProvinceId(editingFacility.provinceId);
        }
    }, [editingId, editingFacility, form]);

    const openCreateModal = () => {
        setEditingId(null);
        setSelectedProvinceId(null);
        form.resetFields();
        setModalOpen(true);
    };

    const openEditModal = (record) => {
        setEditingId(record.id);
        setModalOpen(true);
        // form.setFieldsValue happens in the useEffect above, once the
        // detailed record has actually finished loading — not here, since
        // record from the table row is the lighter list-shape, not
        // guaranteed to have every field the form needs.
    };

    const handleCancel = () => {
        setModalOpen(false);
        form.resetFields();
    };

    const handleFinish = async (values) => {
        try {
            if (editingId) {
                await updateMutation.mutateAsync({ id: editingId, payload: values });
            } else {
                await createMutation.mutateAsync(values);
            }
            message.success('با موفقیت ذخیره شد');
            setModalOpen(false);
            form.resetFields();
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطایی رخ داد');
        }
    };

    const handleDelete = async (id) => {
        try {
            await deleteMutation.mutateAsync(id);
            message.success('با موفقیت حذف شد');
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطا در حذف');
        }
    };

    const columns = [
        { title: 'نام', dataIndex: 'name', key: 'name' },
        { title: 'نوع', dataIndex: 'facilityType', key: 'facilityType', render: (v) => FACILITY_TYPE_LABELS[v] || v },
        { title: 'استان', dataIndex: 'provinceName', key: 'provinceName' },
        { title: 'شهر', dataIndex: 'cityName', key: 'cityName' },
        { title: 'آدرس', dataIndex: 'address', key: 'address' },
        {
            title: 'عملیات', key: 'actions',
            render: (_, record) => (
                <Space>
                    <Button icon={<EditOutlined />} size="small" onClick={() => openEditModal(record)} />
                    <Popconfirm title="آیا از حذف این مورد مطمئن هستید؟" onConfirm={() => handleDelete(record.id)} okText="بله" cancelText="خیر">
                        <Button icon={<DeleteOutlined />} size="small" danger />
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
                <Button type="primary" icon={<PlusOutlined />} onClick={openCreateModal}>افزودن مکان جدید</Button>
            </div>

            <Select
                placeholder="فیلتر بر اساس نوع"
                allowClear
                style={{ width: 200, marginBottom: 16 }}
                value={facilityType}
                onChange={setFacilityType}
                options={[
                    { value: 'Msy', label: 'MSY' },
                    { value: 'Private', label: 'خصوصی' },
                    { value: 'Gov', label: 'دولتی' },
                ]}
            />

            <Table
                rowKey="id"
                columns={columns}
                dataSource={data?.items}
                loading={isLoading}
                pagination={{ current: page, pageSize: 20, total: data?.count, onChange: setPage }}
            />

            <Modal
                title={editingId ? 'ویرایش مکان' : 'افزودن مکان جدید'}
                open={modalOpen}
                onCancel={handleCancel}
                onOk={() => form.submit()}
                confirmLoading={createMutation.isPending || updateMutation.isPending}
                okText="ذخیره"
                cancelText="انصراف"
                destroyOnClose
            >
                <Form form={form} layout="vertical" onFinish={handleFinish} disabled={isFetchingEditingFacility}>
                    <Form.Item name="facilityType" label="نوع مکان" rules={[{ required: true }]}>
                        <Select options={[
                            { value: 'Msy', label: 'MSY' },
                            { value: 'Private', label: 'خصوصی' },
                            { value: 'Gov', label: 'دولتی' },
                        ]} />
                    </Form.Item>
                    <Form.Item name="name" label="نام" rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                    <Form.Item label="استان">
                        <Select
                            placeholder="ابتدا استان را انتخاب کنید"
                            value={selectedProvinceId}
                            options={provinces?.map((p) => ({ value: p.id, label: p.name }))}
                            onChange={(val) => {
                                setSelectedProvinceId(val);
                                form.setFieldValue('cityId', undefined);
                            }}
                        />
                    </Form.Item>
                    <Form.Item name="cityId" label="شهر" rules={[{ required: true }]}>
                        <Select
                            placeholder="شهر را انتخاب کنید"
                            disabled={!selectedProvinceId}
                            options={cities?.map((c) => ({ value: c.id, label: c.name }))}
                        />
                    </Form.Item>
                    <Form.Item name="address" label="آدرس" rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                    <Form.Item name="sportType" label="نوع ورزش">
                        <Input />
                    </Form.Item>
                    <Form.Item name="ownerName" label="نام مالک">
                        <Input />
                    </Form.Item>
                    <Form.Item name="ownerContact" label="تماس مالک">
                        <Input />
                    </Form.Item>
                </Form>
            </Modal>
        </div>
    );
}
