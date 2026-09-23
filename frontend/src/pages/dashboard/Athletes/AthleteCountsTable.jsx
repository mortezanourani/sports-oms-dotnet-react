import { useState } from 'react';
import {
    Table, Button, Space, Popconfirm, message, notification,
    Modal, Form, Select, InputNumber, Alert,
} from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import {
    useAthleteCounts, useAthleteCount, useCreateAthleteCount,
    useUpdateAthleteCount, useDeleteAthleteCount,
} from '../../../hooks/useAthleteCounts';
import { useFederations } from '../../../hooks/useFederations';
import { useProvinces, useCities } from '../../../hooks/useLocations';
import { GENDER_LABELS, PERSIAN_MONTHS } from '../../../constants/labels';

export default function AthleteCountsTable({ filters }) {
    const [page, setPage] = useState(1);
    const [innerFilters, setInnerFilters] = useState({});
    const [modalOpen, setModalOpen] = useState(false);
    const [editingId, setEditingId] = useState(null);
    const [selectedProvinceId, setSelectedProvinceId] = useState(null);
    const [form] = Form.useForm();

    // filters (province/city, from the section tab) and innerFilters
    // (federation/year, local to this table) are two independent concerns
    // and simply merge into one query — the backend doesn't care which
    // layer a given param came from.
    const { data, isLoading } = useAthleteCounts({ page, pageSize: 20, ...filters, ...innerFilters });
    const { data: editingRecord, isFetching: isFetchingEditing } = useAthleteCount(editingId);
    const { data: federations } = useFederations();
    const { data: provinces } = useProvinces();
    const { data: cities } = useCities(selectedProvinceId);

    const createMutation = useCreateAthleteCount();
    const updateMutation = useUpdateAthleteCount();
    const deleteMutation = useDeleteAthleteCount();

    const isEdit = Boolean(editingId);

    const openCreate = () => {
        setEditingId(null);
        setSelectedProvinceId(null);
        form.resetFields();
        setModalOpen(true);
    };

    const openEditById = (id) => {
        setEditingId(id);
        setModalOpen(true);
    };

    const handleFinish = async (values) => {
        if (isEdit) {
            try {
                await updateMutation.mutateAsync({ id: editingId, payload: { count: values.count } });
                message.success('با موفقیت به‌روزرسانی شد');
                setModalOpen(false);
            } catch (err) {
                message.error(err.response?.data?.detail || 'خطایی رخ داد');
            }
            return;
        }

        try {
            await createMutation.mutateAsync(values);
            message.success('با موفقیت ثبت شد');
            setModalOpen(false);
        } catch (err) {
            const responseData = err.response?.data;
            if (err.response?.status === 409 && responseData?.existingId) {
                setModalOpen(false);
                notification.warning({
                    message: 'رکورد تکراری',
                    description: responseData.detail,
                    duration: 8,
                    btn: (
                        <Button type="primary" size="small" onClick={() => { notification.destroy(); openEditById(responseData.existingId); }}>
                            ویرایش رکورد موجود
                        </Button>
                    ),
                });
            } else {
                message.error(responseData?.detail || 'خطایی رخ داد');
            }
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
        { title: 'فدراسیون', dataIndex: 'federationName' },
        { title: 'استان', dataIndex: 'provinceName' },
        { title: 'شهر', dataIndex: 'cityName' },
        { title: 'سال', dataIndex: 'year' },
        { title: 'ماه', dataIndex: 'month', render: (v) => PERSIAN_MONTHS[v - 1] || v },
        { title: 'جنسیت', dataIndex: 'gender', render: (v) => GENDER_LABELS[v] || v },
        { title: 'تعداد', dataIndex: 'count' },
        {
            title: 'عملیات', key: 'actions',
            render: (_, record) => (
                <Space>
                    <Button icon={<EditOutlined />} size="small" onClick={() => openEditById(record.id)} />
                    <Popconfirm title="حذف این رکورد؟" onConfirm={() => handleDelete(record.id)} okText="بله" cancelText="خیر">
                        <Button icon={<DeleteOutlined />} size="small" danger />
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    return (
        <div>
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 16 }}>
                <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>ثبت آمار جدید</Button>
                <Space>
                    <Select
                        placeholder="فیلتر فدراسیون" allowClear style={{ width: 200 }}
                        options={federations?.map((f) => ({ value: f.id, label: f.name }))}
                        onChange={(federationId) => setInnerFilters((f) => ({ ...f, federationId }))}
                    />
                    <InputNumber
                        placeholder="سال" style={{ width: 120 }}
                        onChange={(year) => setInnerFilters((f) => ({ ...f, year }))}
                    />
                </Space>
            </div>

            <Table
                rowKey="id"
                columns={columns}
                dataSource={data?.items}
                loading={isLoading}
                pagination={{ current: page, pageSize: 20, total: data?.count, onChange: setPage }}
            />

            <Modal
                title={isEdit ? 'ویرایش آمار' : 'ثبت آمار جدید'}
                open={modalOpen}
                onCancel={() => setModalOpen(false)}
                onOk={() => form.submit()}
                confirmLoading={createMutation.isPending || updateMutation.isPending}
                okText="ذخیره"
                cancelText="انصراف"
                destroyOnClose
                afterOpenChange={(open) => {
                    if (open && isEdit && editingRecord) form.setFieldsValue(editingRecord);
                }}
            >
                {isEdit && (
                    <Alert type="info" showIcon style={{ marginBottom: 16 }} message="در حالت ویرایش، فقط مقدار «تعداد» قابل تغییر است." />
                )}
                <Form form={form} layout="vertical" onFinish={handleFinish} disabled={isFetchingEditing}>
                    <Form.Item name="federationId" label="فدراسیون" rules={[{ required: true }]}>
                        <Select disabled={isEdit} options={federations?.map((f) => ({ value: f.id, label: f.name }))} />
                    </Form.Item>
                    <Form.Item label="استان">
                        <Select
                            disabled={isEdit}
                            placeholder="انتخاب استان"
                            value={selectedProvinceId}
                            options={provinces?.map((p) => ({ value: p.id, label: p.name }))}
                            onChange={(val) => { setSelectedProvinceId(val); form.setFieldValue('cityId', undefined); }}
                        />
                    </Form.Item>
                    <Form.Item name="cityId" label="شهر" rules={[{ required: true }]}>
                        <Select disabled={isEdit || !selectedProvinceId} options={cities?.map((c) => ({ value: c.id, label: c.name }))} />
                    </Form.Item>
                    <Space style={{ width: '100%' }} size="middle">
                        <Form.Item name="year" label="سال" rules={[{ required: true }]} style={{ width: 140 }}>
                            <InputNumber disabled={isEdit} style={{ width: '100%' }} />
                        </Form.Item>
                        <Form.Item name="month" label="ماه" rules={[{ required: true }]} style={{ width: 160 }}>
                            <Select disabled={isEdit} options={PERSIAN_MONTHS.map((name, i) => ({ value: i + 1, label: name }))} />
                        </Form.Item>
                        <Form.Item name="gender" label="جنسیت" rules={[{ required: true }]} style={{ width: 140 }}>
                            <Select disabled={isEdit} options={Object.entries(GENDER_LABELS).map(([value, label]) => ({ value, label }))} />
                        </Form.Item>
                    </Space>
                    <Form.Item name="count" label="تعداد" rules={[{ required: true }]}>
                        <InputNumber min={0} style={{ width: '100%' }} />
                    </Form.Item>
                </Form>
            </Modal>
        </div>
    );
}
