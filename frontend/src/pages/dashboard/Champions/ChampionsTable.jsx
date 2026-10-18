import { useState, useEffect } from 'react';
import { Table, Button, Space, Popconfirm, message, Modal, Form, Input, Select } from 'antd';
import { PlusOutlined, EditOutlined, DeleteOutlined, TrophyOutlined } from '@ant-design/icons';
import {
    useChampions, useChampion, useCreateChampion, useUpdateChampion, useDeleteChampion,
} from '../../../hooks/useChampions';
import { useFederations } from '../../../hooks/useFederations';
import { useProvinces, useCities } from '../../../hooks/useLocations';
import { GENDER_LABELS } from '../../../constants/labels';
import MedalsModal from './MedalsModal';
import SolarDatePicker from '../../../components/SolarDatePicker';
import { toSolarPickerValue, toUtcDateTime } from '../../../utils/dates';

export default function ChampionsTable({ filters }) {
    const [page, setPage] = useState(1);
    const [modalOpen, setModalOpen] = useState(false);
    const [editingId, setEditingId] = useState(null);
    const [medalsModalChampionId, setMedalsModalChampionId] = useState(null);
    const [provinceSelection, setProvinceSelection] = useState(undefined);
    const [form] = Form.useForm();

    const { data, isLoading } = useChampions({ page, pageSize: 20, ...filters });
    const { data: editingChampion, isFetching: isFetchingEditing } = useChampion(editingId);
    const selectedProvinceId = provinceSelection !== undefined
        ? provinceSelection
        : editingChampion?.id === editingId ? editingChampion?.provinceId : null;
    const { data: federations } = useFederations();
    const { data: provinces } = useProvinces();
    const { data: cities } = useCities(selectedProvinceId);

    const createMutation = useCreateChampion();
    const updateMutation = useUpdateChampion();
    const deleteMutation = useDeleteChampion();

    useEffect(() => {
        if (editingId && editingChampion) {
            form.setFieldsValue({
                ...editingChampion,
                birthDate: toSolarPickerValue(editingChampion.birthDate),
            });
        }
    }, [editingId, editingChampion, form]);

    const openCreate = () => {
        setEditingId(null);
        setProvinceSelection(null);
        form.resetFields();
        setModalOpen(true);
    };

    const openEdit = (record) => {
        setProvinceSelection(undefined);
        setEditingId(record.id);
        setModalOpen(true);
    };

    const handleFinish = async (values) => {
        const payload = { ...values, birthDate: toUtcDateTime(values.birthDate) };
        try {
            if (editingId) {
                await updateMutation.mutateAsync({ id: editingId, payload });
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
        { title: 'نام', dataIndex: 'fullName' },
        { title: 'جنسیت', dataIndex: 'gender', render: (v) => GENDER_LABELS[v] || v },
        { title: 'فدراسیون', dataIndex: 'federationName' },
        { title: 'شهر', dataIndex: 'cityName', render: (v) => v || '—' },
        { title: 'تعداد مدال', dataIndex: 'medalCount' },
        {
            title: 'عملیات', key: 'actions',
            render: (_, record) => (
                <Space>
                    <Button icon={<TrophyOutlined />} size="small" onClick={() => setMedalsModalChampionId(record.id)}>مدال‌ها</Button>
                    <Button icon={<EditOutlined />} size="small" onClick={() => openEdit(record)} />
                    <Popconfirm title="حذف این قهرمان؟ (تمام مدال‌های او نیز حذف می‌شود)" onConfirm={() => handleDelete(record.id)} okText="بله" cancelText="خیر">
                        <Button icon={<DeleteOutlined />} size="small" danger />
                    </Popconfirm>
                </Space>
            ),
        },
    ];

    return (
        <div>
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>افزودن قهرمان جدید</Button>

            <Table
                rowKey="id"
                columns={columns}
                dataSource={data?.items}
                loading={isLoading}
                pagination={{ current: page, pageSize: 20, total: data?.count, onChange: setPage }}
            />

            <Modal
                title={editingId ? 'ویرایش قهرمان' : 'افزودن قهرمان جدید'}
                open={modalOpen}
                onCancel={() => setModalOpen(false)}
                onOk={() => form.submit()}
                confirmLoading={createMutation.isPending || updateMutation.isPending}
                okText="ذخیره"
                cancelText="انصراف"
                destroyOnClose
            >
                <Form form={form} layout="vertical" onFinish={handleFinish} disabled={isFetchingEditing}>
                    <Form.Item name="fullName" label="نام کامل" rules={[{ required: true }]}>
                        <Input />
                    </Form.Item>
                    <Form.Item name="gender" label="جنسیت" rules={[{ required: true }]}>
                        <Select options={Object.entries(GENDER_LABELS).map(([value, label]) => ({ value, label }))} />
                    </Form.Item>
                    <Form.Item name="birthDate" label="تاریخ تولد">
                        <SolarDatePicker />
                    </Form.Item>
                    <Form.Item name="federationId" label="فدراسیون" rules={[{ required: true }]}>
                        <Select options={federations?.map((f) => ({ value: f.id, label: f.name }))} />
                    </Form.Item>
                    <Form.Item label="استان">
                        <Select
                            placeholder="انتخاب استان" allowClear
                            value={selectedProvinceId}
                            options={provinces?.map((p) => ({ value: p.id, label: p.name }))}
                            onChange={(val) => { setProvinceSelection(val); form.setFieldValue('cityId', undefined); }}
                        />
                    </Form.Item>
                    <Form.Item name="cityId" label="شهر">
                        <Select placeholder="شهر (اختیاری)" allowClear disabled={!selectedProvinceId} options={cities?.map((c) => ({ value: c.id, label: c.name }))} />
                    </Form.Item>
                </Form>
            </Modal>

            {medalsModalChampionId && (
                <MedalsModal championId={medalsModalChampionId} open={!!medalsModalChampionId} onClose={() => setMedalsModalChampionId(null)} />
            )}
        </div>
    );
}
