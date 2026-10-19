import { useEffect, useState } from 'react';
import {
    Table, Button, Row, Col, message, Modal, Form, Select, InputNumber,
} from 'antd';
import { EditOutlined } from '@ant-design/icons';
import {
    useAthleteCount, useCreateAthleteCount, useAthleteCountSummary, useUpdateAthleteCount,
} from '../../../hooks/useAthleteCounts';
import { useFederations } from '../../../hooks/useFederations';
import { useProvinces, useCities } from '../../../hooks/useLocations';
import { PERSIAN_MONTHS } from '../../../constants/labels';

export default function AthleteCountsTable({ filters, renderToolbar, canEdit }) {
    const [modalOpen, setModalOpen] = useState(false);
    const [editingId, setEditingId] = useState(null);
    const [selectedProvinceId, setSelectedProvinceId] = useState(null);
    const [form] = Form.useForm();

    const { data, isLoading } = useAthleteCountSummary(filters);
    const { data: editingRecord, isFetching: isFetchingEditing } = useAthleteCount(editingId);
    const { data: federations } = useFederations();
    const { data: provinces } = useProvinces();
    const { data: cities } = useCities(selectedProvinceId);

    const createMutation = useCreateAthleteCount();
    const updateMutation = useUpdateAthleteCount();

    useEffect(() => {
        if (modalOpen && editingId && editingRecord)
            form.setFieldsValue({
                maleCount: editingRecord.maleCount,
                femaleCount: editingRecord.femaleCount,
            });
    }, [modalOpen, editingId, editingRecord, form]);

    const openCreate = () => {
        setEditingId(null);
        setSelectedProvinceId(filters.provinceId || null);
        form.resetFields();
        setModalOpen(true);
    };

    const openEdit = (recordId) => {
        setEditingId(recordId);
        form.resetFields();
        setModalOpen(true);
    };

    const handleFinish = async (values) => {
        try {
            if (editingId) {
                await updateMutation.mutateAsync({
                    id: editingId,
                    payload: { maleCount: values.maleCount, femaleCount: values.femaleCount },
                });
                message.success('با موفقیت به‌روزرسانی شد');
            } else {
                await createMutation.mutateAsync(values);
                message.success('با موفقیت ثبت شد');
            }
            setModalOpen(false);
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطایی رخ داد');
        }
    };

    const columns = [
        { title: 'فدراسیون', dataIndex: 'federationName' },
        {
            title: 'مجموع آقایان',
            dataIndex: 'maleCount',
            render: (count) => count,
        },
        { title: 'مجموع بانوان', dataIndex: 'femaleCount' },
        { title: 'مجموع کل', dataIndex: 'totalCount' },
        ...(canEdit ? [{
            title: 'عملیات',
            key: 'actions',
            render: (_, record) => record.recordId && (
                <Button
                    icon={<EditOutlined />}
                    size="small"
                    onClick={() => openEdit(record.recordId)}
                />
            ),
        }] : []),
    ];

    return (
        <div>
            {renderToolbar?.({ onAdd: openCreate })}

            <Table
                rowKey="federationId"
                columns={columns}
                dataSource={data}
                loading={isLoading}
                pagination={false}
            />

            <Modal
                title={editingId ? 'ویرایش آمار' : 'ثبت آمار جدید'}
                open={modalOpen}
                onCancel={() => setModalOpen(false)}
                onOk={() => form.submit()}
                confirmLoading={createMutation.isPending || updateMutation.isPending}
                okText="ذخیره"
                cancelText="انصراف"
                destroyOnClose
            >
                <Form form={form} layout="vertical" onFinish={handleFinish} disabled={isFetchingEditing}>
                    {!editingId && (
                        <>
                            <Form.Item name="federationId" label="فدراسیون" rules={[{ required: true }]}>
                                <Select options={federations?.map((f) => ({ value: f.id, label: f.displayName }))} />
                            </Form.Item>
                            <Form.Item label="استان">
                                <Select
                                    placeholder="انتخاب استان"
                                    value={selectedProvinceId}
                                    options={provinces?.map((p) => ({ value: p.id, label: p.name }))}
                                    onChange={(val) => { setSelectedProvinceId(val); form.setFieldValue('cityId', undefined); }}
                                />
                            </Form.Item>
                            <Form.Item name="cityId" label="شهر" rules={[{ required: true }]}>
                                <Select disabled={!selectedProvinceId} options={cities?.map((c) => ({ value: c.id, label: c.name }))} />
                            </Form.Item>
                            <Row gutter={16}>
                                <Col span={12}>
                                    <Form.Item name="year" label="سال" rules={[{ required: true }]}>
                                        <InputNumber style={{ width: '100%' }} />
                                    </Form.Item>
                                </Col>
                                <Col span={12}>
                                    <Form.Item name="month" label="ماه" rules={[{ required: true }]}>
                                        <Select options={PERSIAN_MONTHS.map((name, i) => ({ value: i + 1, label: name }))} />
                                    </Form.Item>
                                </Col>
                            </Row>
                        </>
                    )}
                    <Row gutter={16}>
                        <Col span={12}>
                            <Form.Item name="maleCount" label="تعداد آقایان" rules={[{ required: true }]}>
                                <InputNumber min={0} style={{ width: '100%' }} />
                            </Form.Item>
                        </Col>
                        <Col span={12}>
                            <Form.Item name="femaleCount" label="تعداد بانوان" rules={[{ required: true }]}>
                                <InputNumber min={0} style={{ width: '100%' }} />
                            </Form.Item>
                        </Col>
                    </Row>
                </Form>
            </Modal>
        </div>
    );
}
